/*
 * kos-teclas - mini plugin de OBS de Keyboard Overlay Studio
 *
 * Hace que overlay.html se ilumine sin tener abierto el programa ni instalar input-overlay:
 *   - Lee teclado (y botones del raton si el diseño los usa) con Raw Input: no se mete en
 *     medio del juego ni le añade retraso.
 *   - Sirve un WebSocket en 127.0.0.1:16899 con los mismos mensajes que input-overlay:
 *       {"event_type":"key_pressed","keycode":30}   {"event_type":"mouse_released","button":1}
 *   - overlay.html le dice si necesita el raton mandando el texto "mouse:1" o "mouse:0".
 *   - Si el puerto esta ocupado (el programa abierto o input-overlay) lo vuelve a intentar cada 3 s.
 *
 * No usa ninguna funcion de OBS: solo las exportaciones minimas para que OBS lo cargue.
 * Todo corre en un unico hilo propio que duerme mientras no pulsas nada.
 *
 * Compilar (MinGW-w64):  x86_64-w64-mingw32-gcc -O2 -shared -o kos-teclas.dll kos-teclas.c -lws2_32 -ladvapi32 -lcrypt32 -static-libgcc
 */
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <wincrypt.h>
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>

#define EXPORT __declspec(dllexport)
#define PORT 16899
#define MAXC 16
#define WM_SOCK (WM_APP + 1)
#define WM_QUITLOOP (WM_APP + 2)
#define MOUSE 0x10000

/* ---------------- exportaciones que OBS necesita para cargar el modulo ---------------- */
static void *g_module;
EXPORT void obs_module_set_pointer(void *module) { g_module = module; }
EXPORT void *obs_current_module(void) { return g_module; }
EXPORT uint32_t obs_module_ver(void) { return (27u << 24) | (0u << 16) | 0u; } /* API antigua: la aceptan todas las versiones nuevas */
EXPORT const char *obs_module_name(void) { return "Keyboard Overlay Studio - teclas"; }
EXPORT const char *obs_module_description(void) { return "Manda tus teclas a overlay.html de Keyboard Overlay Studio (sin input-overlay)."; }

/* ---------------- estado ---------------- */
typedef struct { SOCKET s; int ready; char buf[4096]; int len; int mouse; } client_t;
static client_t g_cli[MAXC];
static SOCKET g_listen = INVALID_SOCKET;
static HWND g_wnd;
static HANDLE g_thread;
static int g_mouse_on;
static unsigned char g_held[0x10000 / 8 + 1], g_mheld;   /* sin auto-repeticion */

/* ---------------- utilidades ---------------- */
static int keycode(int make, int e0)
{
    if (make == 0x45) return e0 ? 0x45 : 0x0E45;          /* Bloq Num / Pausa */
    if (!e0) return make;
    if (make == 0x48 || make == 0x4B || make == 0x4D || make == 0x50) return 0xE000 | make;
    return 0x0E00 | make;
}
static void send_frame(client_t *c, int op, const char *data, int n)
{
    unsigned char hdr[4]; int h = 2;
    hdr[0] = (unsigned char)(0x80 | op);
    if (n < 126) hdr[1] = (unsigned char)n;
    else { hdr[1] = 126; hdr[2] = (unsigned char)(n >> 8); hdr[3] = (unsigned char)n; h = 4; }
    char out[4200];
    if (n + h > (int)sizeof(out)) return;
    memcpy(out, hdr, h); memcpy(out + h, data, n);
    if (send(c->s, out, n + h, 0) == SOCKET_ERROR && WSAGetLastError() != WSAEWOULDBLOCK) { closesocket(c->s); c->s = INVALID_SOCKET; }
}
static void broadcast(int vc, int down)
{
    char j[96];
    int n = vc >= MOUSE
        ? snprintf(j, sizeof j, "{\"event_type\":\"%s\",\"button\":%d}", down ? "mouse_pressed" : "mouse_released", vc - MOUSE)
        : snprintf(j, sizeof j, "{\"event_type\":\"%s\",\"keycode\":%d}", down ? "key_pressed" : "key_released", vc);
    for (int i = 0; i < MAXC; i++) if (g_cli[i].s != INVALID_SOCKET && g_cli[i].ready) send_frame(&g_cli[i], 1, j, n);
}

/* raton: solo se registra si algun overlay.html lo pide (los ratones de 1000-8000 Hz mandan miles de avisos) */
static void update_mouse(void)
{
    int want = 0;
    for (int i = 0; i < MAXC; i++) if (g_cli[i].s != INVALID_SOCKET && g_cli[i].mouse) want = 1;
    if (want == g_mouse_on) return;
    RAWINPUTDEVICE d = { 1, 2, want ? RIDEV_INPUTSINK : RIDEV_REMOVE, want ? g_wnd : NULL };
    if (RegisterRawInputDevices(&d, 1, sizeof d)) g_mouse_on = want;
}

/* ---------------- saludo WebSocket ---------------- */
static int ws_accept_key(const char *key, char *out, DWORD outlen)
{
    char in[128]; BYTE hash[20]; DWORD hl = 20;
    HCRYPTPROV prov = 0; HCRYPTHASH h = 0; int ok = 0;
    snprintf(in, sizeof in, "%s258EAFA5-E914-47DA-95CA-C5AB0DC85B11", key);
    if (CryptAcquireContextA(&prov, NULL, NULL, PROV_RSA_FULL, CRYPT_VERIFYCONTEXT) &&
        CryptCreateHash(prov, CALG_SHA1, 0, 0, &h) &&
        CryptHashData(h, (BYTE *)in, (DWORD)strlen(in), 0) &&
        CryptGetHashParam(h, HP_HASHVAL, hash, &hl, 0) &&
        CryptBinaryToStringA(hash, 20, CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, out, &outlen)) ok = 1;
    if (h) CryptDestroyHash(h);
    if (prov) CryptReleaseContext(prov, 0);
    return ok;
}
static void client_read(client_t *c)
{
    int n = recv(c->s, c->buf + c->len, (int)sizeof(c->buf) - 1 - c->len, 0);
    if (n <= 0) { if (n == 0 || WSAGetLastError() != WSAEWOULDBLOCK) { closesocket(c->s); c->s = INVALID_SOCKET; update_mouse(); } return; }
    c->len += n; c->buf[c->len] = 0;
    if (!c->ready)
    {
        if (!strstr(c->buf, "\r\n\r\n")) { if (c->len >= (int)sizeof(c->buf) - 1) { closesocket(c->s); c->s = INVALID_SOCKET; } return; }
        char *k = strstr(c->buf, "Sec-WebSocket-Key:"); if (!k) k = strstr(c->buf, "sec-websocket-key:");
        char key[64] = {0}, acc[64] = {0};
        if (k) { k += 18; while (*k == ' ') k++; int i = 0; while (*k && *k != '\r' && i < 63) key[i++] = *k++; }
        if (!k || !ws_accept_key(key, acc, sizeof acc)) { closesocket(c->s); c->s = INVALID_SOCKET; return; }
        char resp[256];
        int rl = snprintf(resp, sizeof resp, "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: %s\r\n\r\n", acc);
        send(c->s, resp, rl, 0);
        c->ready = 1; c->len = 0;
        return;
    }
    /* tramas del navegador: "mouse:1/0", ping y cierre */
    for (;;)
    {
        if (c->len < 2) return;
        unsigned char *b = (unsigned char *)c->buf;
        int op = b[0] & 0x0F, masked = b[1] & 0x80, len = b[1] & 0x7F, p = 2;
        if (len == 126) { if (c->len < 4) return; len = (b[2] << 8) | b[3]; p = 4; }
        else if (len == 127) { closesocket(c->s); c->s = INVALID_SOCKET; return; }
        if (c->len < p + (masked ? 4 : 0) + len) return;
        unsigned char m[4] = {0};
        if (masked) { memcpy(m, b + p, 4); p += 4; }
        for (int i = 0; i < len; i++) b[p + i] ^= m[i & 3];
        if (op == 8) { closesocket(c->s); c->s = INVALID_SOCKET; update_mouse(); return; }
        if (op == 9) send_frame(c, 10, (char *)b + p, len);
        if (op == 1 && len >= 7 && memcmp(b + p, "mouse:", 6) == 0) { c->mouse = b[p + 6] == '1'; update_mouse(); }
        int used = p + len;
        memmove(c->buf, c->buf + used, c->len - used); c->len -= used;
    }
}

/* ---------------- servidor ---------------- */
static void try_listen(void)
{
    if (g_listen != INVALID_SOCKET) return;
    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (s == INVALID_SOCKET) return;
    BOOL ex = TRUE; setsockopt(s, SOL_SOCKET, SO_EXCLUSIVEADDRUSE, (char *)&ex, sizeof ex);
    struct sockaddr_in a; memset(&a, 0, sizeof a);
    a.sin_family = AF_INET; a.sin_port = htons(PORT); a.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    if (bind(s, (struct sockaddr *)&a, sizeof a) || listen(s, 8)) { closesocket(s); return; }   /* ocupado: se reintenta */
    WSAAsyncSelect(s, g_wnd, WM_SOCK, FD_ACCEPT);
    g_listen = s;
}
static void on_socket(SOCKET s, int ev)
{
    if (ev == FD_ACCEPT && s == g_listen)
    {
        SOCKET n = accept(g_listen, NULL, NULL);
        if (n == INVALID_SOCKET) return;
        for (int i = 0; i < MAXC; i++)
            if (g_cli[i].s == INVALID_SOCKET)
            {
                memset(&g_cli[i], 0, sizeof g_cli[i]); g_cli[i].s = n;
                BOOL nd = TRUE; setsockopt(n, IPPROTO_TCP, TCP_NODELAY, (char *)&nd, sizeof nd);
                WSAAsyncSelect(n, g_wnd, WM_SOCK, FD_READ | FD_CLOSE);
                return;
            }
        closesocket(n);
        return;
    }
    for (int i = 0; i < MAXC; i++)
        if (g_cli[i].s == s)
        {
            if (ev == FD_CLOSE) { closesocket(s); g_cli[i].s = INVALID_SOCKET; update_mouse(); }
            else client_read(&g_cli[i]);
            return;
        }
}

/* ---------------- entrada ---------------- */
static void key(int vc, int down)
{
    if (vc >= MOUSE)
    {
        unsigned char bit = (unsigned char)(1 << (vc - MOUSE));
        if (down ? (g_mheld & bit) : !(g_mheld & bit)) return;
        g_mheld = down ? (g_mheld | bit) : (g_mheld & ~bit);
    }
    else
    {
        unsigned char *b = &g_held[vc >> 3], bit = (unsigned char)(1 << (vc & 7));
        if (down ? (*b & bit) : !(*b & bit)) return;
        *b = down ? (*b | bit) : (*b & ~bit);
    }
    broadcast(vc, down);
}
static void on_input(HRAWINPUT h)
{
    RAWINPUT ri; UINT sz = sizeof ri;
    if (GetRawInputData(h, RID_INPUT, &ri, &sz, sizeof(RAWINPUTHEADER)) == (UINT)-1) return;
    if (ri.header.dwType == RIM_TYPEKEYBOARD)
    {
        int make = ri.data.keyboard.MakeCode, fl = ri.data.keyboard.Flags;
        if (make == 0 || make >= 0xFF || (fl & RI_KEY_E1)) return;
        key(keycode(make, (fl & RI_KEY_E0) != 0), !(fl & RI_KEY_BREAK));
    }
    else if (ri.header.dwType == RIM_TYPEMOUSE)
    {
        int f = ri.data.mouse.usButtonFlags;
        if (!f) return;   /* movimiento: fuera */
        for (int i = 0; i < 5; i++)
        {
            if (f & (1 << (i * 2))) key(MOUSE + i + 1, 1);
            if (f & (2 << (i * 2))) key(MOUSE + i + 1, 0);
        }
    }
}

static LRESULT CALLBACK wndproc(HWND w, UINT m, WPARAM wp, LPARAM lp)
{
    switch (m)
    {
    case WM_INPUT: on_input((HRAWINPUT)lp); break;
    case WM_SOCK: on_socket((SOCKET)wp, WSAGETSELECTEVENT(lp)); return 0;
    case WM_TIMER: try_listen(); return 0;
    case WM_QUITLOOP: PostQuitMessage(0); return 0;
    }
    return DefWindowProcA(w, m, wp, lp);
}

static DWORD WINAPI worker(LPVOID arg)
{
    (void)arg;
    WSADATA wd; WSAStartup(MAKEWORD(2, 2), &wd);
    for (int i = 0; i < MAXC; i++) g_cli[i].s = INVALID_SOCKET;
    WNDCLASSA wc; memset(&wc, 0, sizeof wc);
    wc.lpfnWndProc = wndproc; wc.hInstance = GetModuleHandleA(NULL); wc.lpszClassName = "kos-teclas";
    RegisterClassA(&wc);
    g_wnd = CreateWindowExA(0, "kos-teclas", "kos-teclas", WS_POPUP, 0, 0, 0, 0, NULL, NULL, wc.hInstance, NULL);
    if (g_wnd)
    {
        RAWINPUTDEVICE d = { 1, 6, RIDEV_INPUTSINK, g_wnd };   /* teclado, tambien sin foco */
        RegisterRawInputDevices(&d, 1, sizeof d);
        try_listen();
        SetTimer(g_wnd, 1, 3000, NULL);   /* reintentar el puerto si estaba ocupado */
        MSG msg;
        while (GetMessageA(&msg, NULL, 0, 0) > 0) { TranslateMessage(&msg); DispatchMessageA(&msg); }
        RAWINPUTDEVICE r[2] = { { 1, 6, RIDEV_REMOVE, NULL }, { 1, 2, RIDEV_REMOVE, NULL } };
        RegisterRawInputDevices(r, g_mouse_on ? 2 : 1, sizeof r[0]);
        KillTimer(g_wnd, 1);
        DestroyWindow(g_wnd); g_wnd = NULL;
    }
    for (int i = 0; i < MAXC; i++) if (g_cli[i].s != INVALID_SOCKET) { closesocket(g_cli[i].s); g_cli[i].s = INVALID_SOCKET; }
    if (g_listen != INVALID_SOCKET) { closesocket(g_listen); g_listen = INVALID_SOCKET; }
    UnregisterClassA("kos-teclas", wc.hInstance);
    WSACleanup();
    return 0;
}

EXPORT bool obs_module_load(void)
{
    g_thread = CreateThread(NULL, 0, worker, NULL, 0, NULL);
    return g_thread != NULL;
}
EXPORT void obs_module_unload(void)
{
    if (!g_thread) return;
    for (int i = 0; i < 100 && !g_wnd; i++) Sleep(10);
    if (g_wnd) PostMessageA(g_wnd, WM_QUITLOOP, 0, 0);
    WaitForSingleObject(g_thread, 2000);
    CloseHandle(g_thread); g_thread = NULL;
}
