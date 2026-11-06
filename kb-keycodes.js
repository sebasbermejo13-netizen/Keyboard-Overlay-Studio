/* =====================================================================
   Keyboard Overlay Studio - tabla de teclas
   Relaciona la tecla del navegador (e.code) con el codigo VC_* de
   libuiohook, que es lo que manda el plugin input-overlay de OBS.
   Los botones del raton usan 0x10000 + numero de boton.
   ===================================================================== */
(function () {
    var MOUSE = 0x10000;
    var list = [
        // [e.code, codigo VC, nombre visible]
        ["Escape", 0x0001, "Esc"],
        ["F1", 0x003B, "F1"], ["F2", 0x003C, "F2"], ["F3", 0x003D, "F3"], ["F4", 0x003E, "F4"],
        ["F5", 0x003F, "F5"], ["F6", 0x0040, "F6"], ["F7", 0x0041, "F7"], ["F8", 0x0042, "F8"],
        ["F9", 0x0043, "F9"], ["F10", 0x0044, "F10"], ["F11", 0x0057, "F11"], ["F12", 0x0058, "F12"],
        ["Backquote", 0x0029, "º"],
        ["Digit1", 0x0002, "1"], ["Digit2", 0x0003, "2"], ["Digit3", 0x0004, "3"], ["Digit4", 0x0005, "4"],
        ["Digit5", 0x0006, "5"], ["Digit6", 0x0007, "6"], ["Digit7", 0x0008, "7"], ["Digit8", 0x0009, "8"],
        ["Digit9", 0x000A, "9"], ["Digit0", 0x000B, "0"],
        ["Minus", 0x000C, "'"], ["Equal", 0x000D, "¡"], ["Backspace", 0x000E, "Borrar"],
        ["Tab", 0x000F, "Tab"],
        ["KeyQ", 0x0010, "Q"], ["KeyW", 0x0011, "W"], ["KeyE", 0x0012, "E"], ["KeyR", 0x0013, "R"],
        ["KeyT", 0x0014, "T"], ["KeyY", 0x0015, "Y"], ["KeyU", 0x0016, "U"], ["KeyI", 0x0017, "I"],
        ["KeyO", 0x0018, "O"], ["KeyP", 0x0019, "P"],
        ["BracketLeft", 0x001A, "`"], ["BracketRight", 0x001B, "+"], ["Enter", 0x001C, "Enter"],
        ["CapsLock", 0x003A, "Bloq Mayús"],
        ["KeyA", 0x001E, "A"], ["KeyS", 0x001F, "S"], ["KeyD", 0x0020, "D"], ["KeyF", 0x0021, "F"],
        ["KeyG", 0x0022, "G"], ["KeyH", 0x0023, "H"], ["KeyJ", 0x0024, "J"], ["KeyK", 0x0025, "K"],
        ["KeyL", 0x0026, "L"], ["Semicolon", 0x0027, "Ñ"], ["Quote", 0x0028, "´"], ["Backslash", 0x002B, "Ç"],
        ["ShiftLeft", 0x002A, "Shift izq."], ["IntlBackslash", 0x0056, "<"],
        ["KeyZ", 0x002C, "Z"], ["KeyX", 0x002D, "X"], ["KeyC", 0x002E, "C"], ["KeyV", 0x002F, "V"],
        ["KeyB", 0x0030, "B"], ["KeyN", 0x0031, "N"], ["KeyM", 0x0032, "M"],
        ["Comma", 0x0033, ","], ["Period", 0x0034, "."], ["Slash", 0x0035, "-"],
        ["ShiftRight", 0x0036, "Shift der."],
        ["ControlLeft", 0x001D, "Ctrl izq."], ["MetaLeft", 0x0E5B, "Windows"], ["AltLeft", 0x0038, "Alt"],
        ["Space", 0x0039, "Espacio"],
        ["AltRight", 0x0E38, "Alt Gr"], ["MetaRight", 0x0E5C, "Windows der."], ["ContextMenu", 0x0E5D, "Menú"],
        ["ControlRight", 0x0E1D, "Ctrl der."],
        ["PrintScreen", 0x0E37, "Impr Pant"], ["ScrollLock", 0x0046, "Bloq Despl"], ["Pause", 0x0E45, "Pausa"],
        ["Insert", 0x0E52, "Insert"], ["Home", 0x0E47, "Inicio"], ["PageUp", 0x0E49, "Re Pág"],
        ["Delete", 0x0E53, "Supr"], ["End", 0x0E4F, "Fin"], ["PageDown", 0x0E51, "Av Pág"],
        ["ArrowUp", 0xE048, "Flecha arriba"], ["ArrowLeft", 0xE04B, "Flecha izquierda"],
        ["ArrowDown", 0xE050, "Flecha abajo"], ["ArrowRight", 0xE04D, "Flecha derecha"],
        ["NumLock", 0x0045, "Bloq Num"], ["NumpadDivide", 0x0E35, "Num /"], ["NumpadMultiply", 0x0037, "Num *"],
        ["NumpadSubtract", 0x004A, "Num -"], ["NumpadAdd", 0x004E, "Num +"], ["NumpadEnter", 0x0E1C, "Num Enter"],
        ["NumpadDecimal", 0x0053, "Num ."],
        ["Numpad0", 0x0052, "Num 0"], ["Numpad1", 0x004F, "Num 1"], ["Numpad2", 0x0050, "Num 2"],
        ["Numpad3", 0x0051, "Num 3"], ["Numpad4", 0x004B, "Num 4"], ["Numpad5", 0x004C, "Num 5"],
        ["Numpad6", 0x004D, "Num 6"], ["Numpad7", 0x0047, "Num 7"], ["Numpad8", 0x0048, "Num 8"],
        ["Numpad9", 0x0049, "Num 9"],
        ["Mouse1", MOUSE + 1, "Clic izquierdo"], ["Mouse2", MOUSE + 2, "Clic derecho"],
        ["Mouse3", MOUSE + 3, "Clic rueda"], ["Mouse4", MOUSE + 4, "Botón lateral 1"],
        ["Mouse5", MOUSE + 5, "Botón lateral 2"]
    ];

    var byCode = {}, byVC = {};
    list.forEach(function (r) {
        var item = { code: r[0], vc: r[1], name: r[2] };
        byCode[r[0]] = item;
        byVC[r[1]] = item;
    });

    window.KBO_KEYS = {
        MOUSE: MOUSE,
        list: list.map(function (r) { return byCode[r[0]]; }),
        fromCode: function (code) { return byCode[code] || null; },
        fromVC: function (vc) { return byVC[vc] || null; },
        // boton del navegador (0 izq, 1 rueda, 2 der, 3, 4) -> VC del plugin (1 izq, 2 der, 3 rueda, 4, 5)
        fromMouseButton: function (b) {
            var map = { 0: 1, 2: 2, 1: 3, 3: 4, 4: 5 };
            return byVC[MOUSE + (map[b] || 1)];
        },
        nameOf: function (vc) {
            var k = byVC[vc];
            return k ? k.name : "Código 0x" + vc.toString(16).toUpperCase();
        }
    };
})();
