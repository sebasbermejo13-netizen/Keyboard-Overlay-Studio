/* =====================================================================
   Keyboard Overlay Studio - dibujo de las teclas
   Lo usan IGUAL el editor (vista previa) y overlay.html (OBS), asi lo
   que ves en el editor es exactamente lo que sale en el stream.
   ===================================================================== */
(function () {
    var DEFAULT = {
        version: 1,
        canvas: { width: 1000, height: 1000 },          // tamaño de la fuente de navegador en OBS
        position: { anchor: "bottom-left", x: 40, y: 40, scale: 1 },
        tilt: { mode: "center", amount: 100 },        // center | left | right | flat
        font: { family: "Quicksand", weight: 700, size: 28, smallSize: 19, letterSpacing: 1.6, iconGap: 8, iconScale: 100, offsetX: 0, offsetY: 0 },
        shape: { radius: 18, border: 2, depth: 8, press: 6 },
        colors: {
            capTop: "#3a3f46ff", capBottom: "#1b1e22ff", border: "#ffffff2e", text: "#ffffffff",
            icon: "#d99a3aff", body: "#1b1e22cc", bodyBottom: "#0d0f1299",
            activeTop: "#f0a24aff", activeBottom: "#ce5b29ff", activeBorder: "#ffd2a0cc",
            activeText: "#2b1400ff", activeBody: "#963e12bf", activeBodyBottom: "#6426088c",
            glow: "#ce5b2973", ripple: "#f0a24aff",
            bar: "#ffffff73", activeBar: "#00000059"
        },
        effects: { body: true, flash: true, ripple: true, glow: true, bounce: true, textShadow: true,
                   pressMode: "sink", ripple2: false, sparks: false, confetti: false, neon: false, textGlow: false, textPop: false, shake: false, speed: 100 },
        image: { src: "", iw: 0, ih: 0, mode: "span", fit: "cover", opacity: 100, pressedOpacity: 35, zoom: 100, offsetX: 0, offsetY: 0,
                 custom: false, cx: 0, cy: 0, w: 0, h: 0, rot: 0, blend: "normal", hue: 0, sat: 0, light: 0, colorize: false, fx: "" },
        keys: [
            { id: "w", label: "W", codes: [0x0011], x: 86, y: 0, w: 80, h: 94 },
            { id: "a", label: "A", codes: [0x001E], x: 0, y: 100, w: 80, h: 94 },
            { id: "s", label: "S", codes: [0x001F], x: 86, y: 100, w: 80, h: 94 },
            { id: "d", label: "D", codes: [0x0020], x: 172, y: 100, w: 80, h: 94 },
            { id: "shift", label: "Shift", icon: "⇧", small: true, codes: [0x002A, 0x0036], x: 258, y: 100, w: 108, h: 94 },
            { id: "space", label: "", bar: true, codes: [0x0039], x: 0, y: 200, w: 180, h: 70 },
            { id: "ctrl", label: "Ctrl", small: true, codes: [0x001D, 0x0E1D], x: 186, y: 200, w: 180, h: 70 }
        ]
    };

    var KEY_DEFAULTS = {
        label: "", icon: "", small: false, bar: false, hideText: false, codes: [],
        x: 0, y: 0, w: 80, h: 94,
        image: "", imageOpacity: 100, imagePressedOpacity: 35, imageFit: "cover"
    };

    // fuentes con sus pesos disponibles en Google Fonts ("" = fuente del sistema)
    var FONTS = {
        "Quicksand": "400;500;600;700", "Poppins": "400;500;600;700;800;900", "Montserrat": "400;500;600;700;800;900",
        "Rajdhani": "400;500;600;700", "Orbitron": "400;500;600;700;800;900", "Russo One": "400",
        "Bebas Neue": "400", "Oswald": "400;500;600;700", "Roboto": "400;500;700;900",
        "Inter": "400;500;600;700;800;900", "Nunito": "400;600;700;800;900", "Fredoka": "400;500;600;700",
        "Exo 2": "400;500;600;700;800;900", "Teko": "400;500;600;700", "Chakra Petch": "400;500;600;700",
        "Press Start 2P": "400", "Bangers": "400", "Permanent Marker": "400", "Lexend": "400;500;600;700;800;900",
        "Cinzel": "400;500;600;700;800;900", "Luckiest Guy": "400", "Anton": "400",
        "Barlow": "400;500;600;700;800;900", "Kanit": "400;500;600;700;800;900", "Outfit": "400;500;600;700;800;900",
        "Space Grotesk": "400;500;600;700", "Righteous": "400", "Audiowide": "400",
        "Black Ops One": "400", "Bungee": "400", "Titillium Web": "400;600;700;900",
        "Rubik": "400;500;600;700;800;900", "VT323": "400", "Silkscreen": "400;700",
        "Lilita One": "400",
        "Arial": "", "Segoe UI": "", "Verdana": "", "Impact": "", "Consolas": ""
    };

    // iconos vectoriales propios ("@muro"...): mismos datos que VIcons en el programa (cuadricula 24x24, [opacidad, poligonos])
    function R(x0, y0, x1, y1) { return [x0, y0, x1, y0, x1, y1, x0, y1]; }
    // capa = [opacidad, poligonos, grosor]: grosor 0 = relleno; > 0 = aristas (efecto cristal)
    var VICONS = {
        "@muro": [[0.42, [[6.16, 15.88, 15.89, 21.5, 15.89, 9.24, 6.16, 3.62]], 0], [0.28, [[17.84, 20.38, 15.89, 21.5, 15.89, 9.24, 17.84, 8.12]], 0], [0.6, [[8.11, 2.5, 17.84, 8.12, 15.89, 9.24, 6.16, 3.62]], 0], [0.35, [[8.11, 14.76, 17.84, 20.38], [8.11, 14.76, 6.16, 15.88], [8.11, 14.76, 8.11, 2.5]], 0.7], [1, [[8.11, 2.5, 17.84, 8.12], [17.84, 8.12, 15.89, 9.24], [15.89, 9.24, 6.16, 3.62], [6.16, 3.62, 8.11, 2.5], [6.16, 15.88, 15.89, 21.5], [6.16, 15.88, 6.16, 3.62], [15.89, 21.5, 15.89, 9.24], [17.84, 20.38, 15.89, 21.5], [17.84, 20.38, 17.84, 8.12]], 1.1]],
        "@suelo": [[0.42, [[2.5, 13.1, 12.0, 18.58, 12.0, 16.39, 2.5, 10.9]], 0], [0.28, [[21.5, 13.1, 12.0, 18.58, 12.0, 16.39, 21.5, 10.9]], 0], [0.6, [[12.0, 5.42, 21.5, 10.9, 12.0, 16.39, 2.5, 10.9]], 0], [0.35, [[12.0, 7.61, 21.5, 13.1], [12.0, 7.61, 2.5, 13.1], [12.0, 7.61, 12.0, 5.42]], 0.7], [1, [[12.0, 5.42, 21.5, 10.9], [21.5, 10.9, 12.0, 16.39], [12.0, 16.39, 2.5, 10.9], [2.5, 10.9, 12.0, 5.42], [2.5, 13.1, 12.0, 18.58], [2.5, 13.1, 2.5, 10.9], [12.0, 18.58, 12.0, 16.39], [21.5, 13.1, 12.0, 18.58], [21.5, 13.1, 21.5, 10.9]], 1.1]],
        "@escalera": [[0.42, [[3.4, 17.25, 10.77, 21.5, 10.77, 18.48, 3.4, 14.22]], 0], [0.6, [[6.68, 12.33, 14.05, 16.58, 10.77, 18.48, 3.4, 14.22]], 0], [0.42, [[6.68, 12.33, 14.05, 16.58, 14.05, 13.56, 6.68, 9.31]], 0], [0.6, [[9.95, 7.42, 17.32, 11.67, 14.05, 13.56, 6.68, 9.31]], 0], [0.42, [[9.95, 7.42, 17.32, 11.67, 17.32, 8.64, 9.95, 4.39]], 0], [0.6, [[13.23, 2.5, 20.6, 6.75, 17.32, 8.64, 9.95, 4.39]], 0], [0.28, [[10.77, 21.5, 10.77, 18.48, 14.05, 16.58, 14.05, 13.56, 17.32, 11.67, 17.32, 8.64, 20.6, 6.75, 20.6, 15.83]], 0], [0.35, [[13.23, 11.57, 20.6, 15.83], [13.23, 11.57, 3.4, 17.25], [13.23, 11.57, 13.23, 2.5]], 0.7], [1, [[10.77, 21.5, 10.77, 18.48], [10.77, 18.48, 14.05, 16.58], [14.05, 16.58, 14.05, 13.56], [14.05, 13.56, 17.32, 11.67], [17.32, 11.67, 17.32, 8.64], [17.32, 8.64, 20.6, 6.75], [20.6, 6.75, 20.6, 15.83], [20.6, 15.83, 10.77, 21.5], [3.4, 17.25, 10.77, 21.5], [3.4, 14.22, 10.77, 18.48], [6.68, 12.33, 14.05, 16.58], [6.68, 9.31, 14.05, 13.56], [9.95, 7.42, 17.32, 11.67], [9.95, 4.39, 17.32, 8.64], [13.23, 2.5, 20.6, 6.75], [3.4, 17.25, 3.4, 14.22], [3.4, 14.22, 6.68, 12.33], [6.68, 12.33, 6.68, 9.31], [6.68, 9.31, 9.95, 7.42], [9.95, 7.42, 9.95, 4.39], [9.95, 4.39, 13.23, 2.5]], 1.1]],
        "@techo": [[0.42, [[2.5, 13.83, 12.0, 19.31, 12.0, 4.69]], 0], [0.28, [[21.5, 13.83, 12.0, 19.31, 12.0, 4.69]], 0], [0.35, [[12.0, 8.34, 21.5, 13.83], [12.0, 8.34, 2.5, 13.83], [12.0, 8.34, 12.0, 4.69]], 0.7], [1, [[21.5, 13.83, 12.0, 19.31], [12.0, 19.31, 2.5, 13.83], [21.5, 13.83, 12.0, 4.69], [12.0, 19.31, 12.0, 4.69], [2.5, 13.83, 12.0, 4.69]], 1.1]],
        "@pico": [[1, [[3.8, 16.2, 3.81, 14.39, 4.02, 12.73, 4.42, 11.21, 5.02, 9.85, 5.82, 8.63, 6.81, 7.57, 8.0, 6.65, 9.38, 5.88, 10.96, 5.26, 12.74, 4.79, 14.71, 4.47, 16.88, 4.3, 19.24, 4.27, 21.8, 4.4, 19.98, 4.7, 18.25, 5.08, 16.6, 5.55, 15.02, 6.1, 13.53, 6.73, 12.13, 7.45, 10.8, 8.25, 9.56, 9.13, 8.39, 10.1, 7.31, 11.16, 6.31, 12.29, 5.39, 13.51, 4.56, 14.81], [6.75, 8.48, 10.27, 5.52, 12.45, 8.12, 8.93, 11.08], [8.8, 8.98, 19.8, 21.88, 21.4, 20.52, 10.4, 7.62]], 0]],
        "@editar": [[1, [R(3, 3, 8.4, 8.4), R(9.3, 3, 14.7, 8.4), R(3, 9.3, 8.4, 14.7), R(9.3, 9.3, 14.7, 14.7), R(15.6, 9.3, 21, 14.7), R(3, 15.6, 8.4, 21), R(9.3, 15.6, 14.7, 21), R(15.6, 15.6, 21, 21)], 0], [0.35, [R(15.6, 3, 21, 8.4)], 0]]
    };
    function vIcon(doc, id) {
        var ns = "http://www.w3.org/2000/svg", svg = doc.createElementNS(ns, "svg");
        svg.setAttribute("viewBox", "0 0 24 24"); svg.setAttribute("width", "1em"); svg.setAttribute("height", "1em");
        VICONS[id].forEach(function (layer) {
            layer[1].forEach(function (pts) {
                var line = layer[2] > 0, pg = doc.createElementNS(ns, line ? "polyline" : "polygon");
                pg.setAttribute("points", pts.join(" "));
                if (line) {
                    pg.setAttribute("fill", "none"); pg.setAttribute("stroke", "currentColor"); pg.setAttribute("stroke-opacity", layer[0]);
                    pg.setAttribute("stroke-width", layer[2]); pg.setAttribute("stroke-linecap", "round"); pg.setAttribute("stroke-linejoin", "round");
                } else { pg.setAttribute("fill", "currentColor"); pg.setAttribute("fill-opacity", layer[0]); }
                svg.appendChild(pg);
            });
        });
        return svg;
    }
    function clone(o) { return JSON.parse(JSON.stringify(o)); }

    // completa lo que falte (configs viejas o editadas a mano)
    function normalize(cfg) {
        var base = clone(DEFAULT);
        cfg = cfg ? clone(cfg) : {};
        ["canvas", "position", "tilt", "font", "shape", "colors", "effects", "image"].forEach(function (k) {
            base[k] = Object.assign(base[k], cfg[k] || {});
        });
        if (Array.isArray(cfg.keys)) base.keys = cfg.keys;
        base.keys = base.keys.map(function (k, i) {
            var o = Object.assign({}, KEY_DEFAULTS, k);
            if (!o.id) o.id = "k" + Date.now().toString(36) + i;
            return o;
        });
        return base;
    }

    function loadFont(doc, family) {
        if (!(family in FONTS) || FONTS[family] === "") {
            if (family in FONTS) return; // fuente del sistema
        }
        var id = "kbo-font-" + family.replace(/\W+/g, "-");
        if (doc.getElementById(id)) return;
        var w = FONTS[family];
        var link = doc.createElement("link");
        link.id = id;
        link.rel = "stylesheet";
        link.href = "https://fonts.googleapis.com/css2?family=" + encodeURIComponent(family).replace(/%20/g, "+") +
            (w ? ":wght@" + w : "") + "&display=swap";
        doc.head.appendChild(link);
    }

    function tiltTransform(t) {
        var a = (t.amount == null ? 100 : t.amount) / 100;
        switch (t.mode) {
            case "left":  return "perspective(1300px) rotateX(" + 22 * a + "deg) rotateZ(" + -8 * a + "deg)";
            case "right": return "perspective(1300px) rotateX(" + 22 * a + "deg) rotateZ(" + 8 * a + "deg)";
            case "side-left":  return "perspective(1000px) rotateY(" + -30 * a + "deg)";
            case "side-right": return "perspective(1000px) rotateY(" + 30 * a + "deg)";
            case "flat":  return "none";
            default:      return "perspective(1000px) rotateX(" + 30 * a + "deg)";
        }
    }

    function buildCSS(c, scope) {
        var col = c.colors, s = c.shape, f = c.font, e = c.effects, S = scope;
        var sp = 100 / Math.max(10, e.speed || 100);
        var d = function (ms) { return (ms * sp / 1000).toFixed(3) + "s"; };
        var release = e.bounce ? "cubic-bezier(0.34, 1.56, 0.64, 1)" : "ease-out";
        var family = '"' + f.family + '", Arial, sans-serif';
        var innerR = Math.max(0, s.radius - s.border);
        var P = e.body ? s.press : 0, pm = e.pressMode || "sink";
        var capT = { sink: "translateY(" + P + "px)", shrink: "translateY(" + (P * 0.3) + "px) scale(.92)", pop: "scale(1.06)",
            tilt: "translateY(" + (P * 0.5) + "px) rotate(-4deg)", jump: "translateY(" + (-Math.max(P, 6)) + "px)", none: "none" }[pm] || "none";
        var ls = Number(f.letterSpacing || 0), ox = Number(f.offsetX || 0), oy = Number(f.offsetY || 0);
        var txtT = "translate(var(--tx,0px),var(--ty,0px)) scale(var(--ts,1))";
        var shadowsActive = "inset 0 4px 6px rgba(0,0,0,.35),inset 0 -2px 0 rgba(255,255,255,.25)" + (e.glow ? ",0 0 22px " + col.glow : "") +
            (e.neon ? ",0 0 6px " + col.ripple + ",0 0 16px " + col.ripple : "");
        return [
            S + "{position:relative;overflow:hidden;font-family:" + family + "}",
            S + " *{box-sizing:border-box;margin:0;padding:0;user-select:none;font-family:inherit}",
            S + " .kbo-wrap{position:absolute}",
            S + " .kbo-keys{position:relative;transform-origin:50% 100%}",
            S + " .kbo-key{position:absolute}",
            S + " .kbo-key::before{content:'';position:absolute;left:0;right:0;top:var(--body-top);bottom:0;border-radius:" + s.radius + "px;" +
                "background:linear-gradient(180deg," + col.body + "," + col.bodyBottom + ");transition:background .12s ease;" + (e.body ? "" : "display:none;") + "}",
            S + " .kbo-key.active::before{background:linear-gradient(180deg," + col.activeBody + "," + col.activeBodyBottom + ")}",
            S + " .kbo-cap{position:absolute;left:0;right:0;top:0;bottom:" + (e.body ? s.depth : 0) + "px;border-radius:" + s.radius + "px;" +
                "background:linear-gradient(180deg," + col.capTop + "," + col.capBottom + ");border:" + s.border + "px solid " + col.border + ";" +
                "box-shadow:inset 0 1px 0 rgba(255,255,255,.09),inset 0 -4px 0 rgba(0,0,0,.30);color:" + col.text + ";" +
                "font-size:" + f.size + "px;font-weight:" + f.weight + ";text-shadow:" + (e.textShadow ? "0 2px 3px #000" : "none") + ";" +
                "transition:transform " + d(220) + " " + release + ",box-shadow .12s ease,color .1s ease,border-color .12s ease}",
            S + " .kbo-cap::before{content:'';position:absolute;inset:0;border-radius:" + innerR + "px;z-index:0;" +
                "background:linear-gradient(180deg," + col.activeTop + "," + col.activeBottom + ");opacity:0;transition:opacity " + d(180) + " ease-out}",
            S + " .kbo-cap::after, " + S + " .kbo-r2{content:'';position:absolute;inset:-" + s.border + "px;border-radius:" + s.radius + "px;border:3px solid " + col.ripple + ";opacity:0;pointer-events:none}",
            S + " .kbo-img{position:absolute;inset:0;z-index:1;border-radius:" + innerR + "px;background-position:center;background-repeat:no-repeat;" +
                "opacity:var(--img-o,1);transition:opacity .12s ease}",
            S + " .kbo-content{position:relative;z-index:2;width:100%;height:100%;overflow:hidden;border-radius:" + innerR + "px;display:flex;align-items:center;justify-content:center;white-space:nowrap}",
            S + " .kbo-txt{display:flex;align-items:center;gap:" + (f.iconGap == null ? 8 : f.iconGap) + "px;flex:none;transform:" + txtT + ";transition:transform " + d(220) + " " + release + "}",
            S + " .kbo-content span{letter-spacing:" + ls + "px;margin-right:" + (-ls) + "px}",
            S + " .kbo-content.upper span{text-transform:uppercase}",
            S + " .kbo-content.lower span{text-transform:lowercase}",
            S + " .kbo-content i svg{display:block;overflow:visible}",
            S + " .kbo-content i{font-style:normal;font-size:" + Math.max(4, (f.size - 4) * (f.iconScale || 100) / 100) + "px;color:" + col.icon + "}",
            S + " .kbo-bar{height:4px;border-radius:4px;background:" + (col.bar || "#ffffff73") + "}",
            S + " .kbo-key.active .kbo-cap{transform:" + capT + ";" +
                "transition:transform " + d(80) + " ease-out,box-shadow .08s ease,color .08s ease,border-color .08s ease;" +
                "border-color:" + (e.neon ? col.ripple : col.activeBorder) + ";color:" + col.activeText + ";text-shadow:none;box-shadow:" + shadowsActive + "}",
            S + " .kbo-key.active .kbo-cap::before{opacity:1;transition:opacity " + d(50) + " ease-out;" + (e.flash ? "animation:kboFlash " + d(320) + " ease-out" : "") + "}",
            S + " .kbo-key.active .kbo-cap::after{" + (e.ripple ? "animation:kboRipple " + d(450) + " ease-out" : "") + "}",
            S + " .kbo-key.active .kbo-r2{" + (e.ripple2 ? "animation:kboRipple " + d(450) + " ease-out " + d(120) : "") + "}",
            S + " .kbo-key.active .kbo-txt{" + (e.textPop ? "transform:translate(var(--tx,0px),var(--ty,0px)) scale(calc(var(--ts,1) * 1.15));" : "") +
                (e.textGlow ? "text-shadow:0 0 4px " + col.glow + ",0 0 10px " + col.glow + ",0 0 18px " + col.glow + ";" : "") + "}",
            S + " .kbo-key.active{" + (e.shake ? "animation:kboShake " + d(260) + " linear" : "") + "}",
            S + " .kbo-fx{position:absolute;left:0;right:0;top:0;pointer-events:none;z-index:5}",
            S + " .kbo-sp{position:absolute;width:6px;height:6px;margin:-3px 0 0 -3px;border-radius:50%;background:" + col.activeTop + ";opacity:0}",
            S + " .kbo-cf{position:absolute;width:6px;height:4px;margin:-2px 0 0 -3px;border-radius:1px;opacity:0}",
            S + " .kbo-key.active .kbo-sp{animation:kboSpark " + d(600) + " ease-out}",
            S + " .kbo-key.active .kbo-cf{animation:kboConf " + d(800) + " ease-out}",
            S + " .kbo-key.active .kbo-img{opacity:var(--img-op,.35)}",
            S + " .kbo-gimg{position:absolute;inset:0;z-index:1;overflow:hidden;border-radius:" + innerR + "px;mix-blend-mode:" + (c.image.blend || "normal") + ";opacity:" + (c.image.opacity / 100) + ";transition:opacity .12s ease}",
            S + " .kbo-gimg-in{position:absolute;background-repeat:no-repeat;background-size:100% 100%}",
            S + " .kbo-kimg{position:absolute;inset:0;z-index:1;overflow:hidden;border-radius:" + innerR + "px;opacity:var(--ki-o,1);transition:opacity .12s ease}",
            S + " .kbo-key.active .kbo-kimg{opacity:var(--ki-op,.35)}",
            S + " .kbo-key.active .kbo-gimg{opacity:" + (c.image.pressedOpacity / 100) + "}",
            S + " .kbo-key.active i{color:" + col.activeText + "}",
            S + " .kbo-key.active .kbo-bar{background:" + (col.activeBar || "#00000059") + "}",
            "@keyframes kboFlash{0%{filter:brightness(1.9) saturate(.7)}100%{filter:brightness(1) saturate(1)}}",
            "@keyframes kboRipple{0%{opacity:.9;transform:scale(.96)}100%{opacity:0;transform:scale(1.32)}}",
            "@keyframes kboShake{0%{transform:translateX(0)}25%{transform:translateX(3px)}50%{transform:translateX(-2px)}75%{transform:translateX(1px)}100%{transform:translateX(0)}}",
            "@keyframes kboSpark{0%{opacity:1;transform:translate(0,0) scale(1)}100%{opacity:0;transform:translate(var(--dx),var(--dy)) scale(.2)}}",
            "@keyframes kboConf{0%{opacity:1;transform:translate(0,0) rotate(0)}100%{opacity:0;transform:translate(var(--dx),calc(var(--dy) + 24px)) rotate(var(--r))}}"
        ].join("\n");
    }

    // mismo calculo que el programa (Config.SpanRect)
    function spanRect(c) {
        var b = bounds(c.keys), im = c.image;
        var iw = Math.max(1, im.iw || 1), ih = Math.max(1, im.ih || 1), z = (im.zoom || 100) / 100, w, h;
        if (im.fit === "stretch") { w = b.w * z; h = b.h * z; }
        else {
            var s = im.fit === "contain" ? Math.min(b.w / iw, b.h / ih) : Math.max(b.w / iw, b.h / ih);
            w = iw * s * z; h = ih * s * z;
        }
        return { x: (b.w - w) / 2 + b.w * (im.offsetX || 0) / 100, y: (b.h - h) / 2 + b.h * (im.offsetY || 0) / 100, w: w, h: h };
    }

    // posicion/tamano/giro de la imagen comun (igual que Config.ImageFrame del programa)
    function imageFrame(c) {
        var im = c.image;
        if (im.custom && im.w > 0 && im.h > 0) return { cx: im.cx, cy: im.cy, w: im.w, h: im.h, rot: im.rot || 0 };
        var r = spanRect(c);
        return { cx: r.x + r.w / 2, cy: r.y + r.h / 2, w: r.w, h: r.h, rot: 0 };
    }

    function bounds(keys) {
        var w = 0, h = 0;
        keys.forEach(function (k) { w = Math.max(w, k.x + k.w); h = Math.max(h, k.y + k.h); });
        return { w: w, h: h };
    }

    /**
     * Dibuja el overlay dentro de "root".
     * opts.flat = true -> ignora la inclinacion (vista de edicion del editor)
     * Devuelve { els: {id: elemento}, keysEl, wrapEl }
     */
    function render(root, cfg, opts) {
        opts = opts || {};
        var c = normalize(cfg);
        var doc = root.ownerDocument;
        if (!root.id) root.id = "kbo-" + Math.random().toString(36).slice(2, 8);
        root.classList.add("kbo-root");
        root.style.width = c.canvas.width + "px";
        root.style.height = c.canvas.height + "px";

        loadFont(doc, c.font.family);

        var styleId = root.id + "-style";
        var st = doc.getElementById(styleId);
        if (!st) { st = doc.createElement("style"); st.id = styleId; doc.head.appendChild(st); }
        st.textContent = buildCSS(c, "#" + root.id);

        root.innerHTML = "";
        var wrap = doc.createElement("div");
        wrap.className = "kbo-wrap";
        var p = c.position, an = p.anchor || "bottom-left";
        var origin = [];
        if (an === "center") {
            wrap.style.left = "50%"; wrap.style.top = "50%";
            wrap.style.transform = "translate(-50%,-50%) scale(" + p.scale + ")";
            origin = ["50%", "50%"];
        } else {
            var v = an.split("-")[0], hz = an.split("-")[1];
            wrap.style[v] = p.y + "px";
            wrap.style[hz] = p.x + "px";
            wrap.style.transform = "scale(" + p.scale + ")";
            origin = [hz, v];
        }
        wrap.style.transformOrigin = origin.join(" ");

        var keysEl = doc.createElement("div");
        keysEl.className = "kbo-keys";
        var b = bounds(c.keys);
        keysEl.style.width = b.w + "px";
        keysEl.style.height = b.h + "px";
        keysEl.style.transform = opts.flat ? "none" : tiltTransform(c.tilt);
        keysEl.style.transformOrigin = c.tilt.mode === "side-right" ? "0% 50%" : c.tilt.mode === "side-left" ? "100% 50%" : "50% 100%";
        wrap.appendChild(keysEl);
        root.appendChild(wrap);

        var els = {};
        c.keys.forEach(function (k) {
            var el = doc.createElement("div");
            el.className = "kbo-key";
            el.dataset.id = k.id;
            el.style.left = k.x + "px";
            el.style.top = k.y + "px";
            el.style.width = k.w + "px";
            el.style.height = k.h + "px";
            var press = c.effects.body ? c.shape.press : 0;
            el.style.setProperty("--body-top", Math.max(press + 4, Math.round(k.h * 0.32)) + "px");

            var cap = doc.createElement("div");
            cap.className = "kbo-cap";

            // imagen comun a todas las teclas (repartida por el teclado o repetida en cada una)
            var gi = c.image, gsrc = gi && (gi.fx || gi.src);
            if (gsrc) {
                var gd = doc.createElement("div");
                gd.className = "kbo-gimg";
                var gin = doc.createElement("div");
                gin.className = "kbo-gimg-in";
                gin.style.backgroundImage = 'url("' + gsrc + '")';
                if (gi.mode === "each") {
                    gin.style.inset = "0";
                    gin.style.backgroundSize = gi.fit === "stretch" ? "100% 100%" : (gi.fit || "cover");
                    gin.style.backgroundPosition = "center";
                } else {
                    var fr = imageFrame(c), bw = c.shape.border;
                    gin.style.left = (fr.cx - fr.w / 2 - k.x - bw) + "px";
                    gin.style.top = (fr.cy - fr.h / 2 - k.y - bw) + "px";
                    gin.style.width = fr.w + "px";
                    gin.style.height = fr.h + "px";
                    if (fr.rot) gin.style.transform = "rotate(" + fr.rot + "deg)";
                }
                gd.appendChild(gin);
                cap.appendChild(gd);
            }
            // imagen propia de la tecla (mismas opciones que la comun). "image" = formato antiguo
            var ki = k.img || (k.image ? { src: k.image, opacity: k.imageOpacity, pressedOpacity: k.imagePressedOpacity, fit: k.imageFit } : null);
            var ksrc = ki && (ki.fx || ki.src);
            if (ksrc) {
                var kd = doc.createElement("div");
                kd.className = "kbo-kimg";
                kd.style.mixBlendMode = ki.blend || "normal";
                el.style.setProperty("--ki-o", (ki.opacity == null ? 100 : ki.opacity) / 100);
                el.style.setProperty("--ki-op", (ki.pressedOpacity == null ? 35 : ki.pressedOpacity) / 100);
                var kin = doc.createElement("div");
                kin.className = "kbo-gimg-in";
                kin.style.backgroundImage = 'url("' + ksrc + '")';
                if (ki.custom && ki.w > 0 && ki.h > 0) {
                    var kb = c.shape.border;
                    kin.style.left = (ki.cx - ki.w / 2 - kb) + "px";
                    kin.style.top = (ki.cy - ki.h / 2 - kb) + "px";
                    kin.style.width = ki.w + "px";
                    kin.style.height = ki.h + "px";
                    if (ki.rot) kin.style.transform = "rotate(" + ki.rot + "deg)";
                } else {
                    kin.style.inset = "0";
                    kin.style.backgroundSize = ki.fit === "stretch" ? "100% 100%" : (ki.fit || "cover");
                    kin.style.backgroundPosition = "center";
                }
                kd.appendChild(kin);
                cap.appendChild(kd);
            }

            var content = doc.createElement("div");
            var tcase = k.textCase || (k.small ? "upper" : "asis");
            content.className = "kbo-content" + (tcase === "upper" ? " upper" : tcase === "lower" ? " lower" : "");
            var pos = k.textPos || "center";
            var hz = pos.indexOf("left") >= 0 ? "left" : pos.indexOf("right") >= 0 ? "right" : "center";
            var vt = pos.indexOf("top") === 0 ? "top" : pos.indexOf("bottom") === 0 ? "bottom" : "center";
            content.style.justifyContent = hz === "left" ? "flex-start" : hz === "right" ? "flex-end" : "center";
            content.style.alignItems = vt === "top" ? "flex-start" : vt === "bottom" ? "flex-end" : "center";
            if (pos !== "center") content.style.padding = "8px";
            var txt = doc.createElement("div");
            txt.className = "kbo-txt";
            txt.style.setProperty("--ts", (k.textScale || 100) / 100);
            txt.style.setProperty("--tx", ((k.textX || 0) + Number(c.font.offsetX || 0)) + "px");
            txt.style.setProperty("--ty", ((k.textY || 0) + Number(c.font.offsetY || 0)) + "px");
            txt.style.transformOrigin = hz + " " + vt;
            if (!k.hideText) {
                if (k.icon) {
                    var i = doc.createElement("i");
                    if (VICONS[k.icon]) { i.style.display = "flex"; i.appendChild(vIcon(doc, k.icon)); } else i.textContent = k.icon;
                    txt.appendChild(i);
                }
                if (k.bar) {
                    var bar = doc.createElement("span");
                    bar.className = "kbo-bar";
                    bar.style.width = Math.max(0, (k.w - 2 * c.shape.border) * 0.44) + "px";
                    txt.appendChild(bar);
                } else if (k.label) {
                    var sp = doc.createElement("span");
                    sp.textContent = k.label;
                    txt.appendChild(sp);
                }
            }
            content.appendChild(txt);
            // segunda onda
            if (c.effects.ripple2) { var r2 = doc.createElement("div"); r2.className = "kbo-r2"; cap.appendChild(r2); }
            // particulas: chispas y confeti (salen del centro de la cara)
            if (c.effects.sparks || c.effects.confetti) {
                var fx = doc.createElement("div");
                fx.className = "kbo-fx";
                var capH = k.h - (c.effects.body ? c.shape.depth : 0);
                fx.style.height = capH + "px";
                var dist = Math.max(k.w, k.h) * 0.75, pi;
                if (c.effects.sparks) for (pi = 0; pi < 10; pi++) {
                    var a1 = pi * 36 * Math.PI / 180, spk = doc.createElement("span");
                    spk.className = "kbo-sp"; spk.style.left = "50%"; spk.style.top = "50%";
                    spk.style.setProperty("--dx", (Math.cos(a1) * dist).toFixed(1) + "px");
                    spk.style.setProperty("--dy", (Math.sin(a1) * dist).toFixed(1) + "px");
                    fx.appendChild(spk);
                }
                if (c.effects.confetti) {
                    var pal = [c.colors.activeTop, c.colors.activeBottom, c.colors.ripple, c.colors.icon, c.colors.glow];
                    for (pi = 0; pi < 14; pi++) {
                        var a2 = pi * 137.5 * Math.PI / 180, dd = dist * (0.5 + 0.4 * ((pi * 7) % 5) / 4), cf = doc.createElement("span");
                        cf.className = "kbo-cf"; cf.style.left = "50%"; cf.style.top = "50%";
                        cf.style.background = pal[pi % pal.length].slice(0, 7);
                        cf.style.setProperty("--dx", (Math.cos(a2) * dd).toFixed(1) + "px");
                        cf.style.setProperty("--dy", (Math.sin(a2) * dd).toFixed(1) + "px");
                        cf.style.setProperty("--r", ((pi % 2 === 0 ? 1 : -1) * (180 + pi * 20)) + "deg");
                        fx.appendChild(cf);
                    }
                }
                el.appendChild(fx);
            }
            cap.appendChild(content);
            el.appendChild(cap);
            keysEl.appendChild(el);
            els[k.id] = el;
        });
        return { els: els, keysEl: keysEl, wrapEl: wrap, config: c };
    }

    // codigo VC -> lista de ids de tecla
    function codeMap(cfg) {
        var m = {};
        normalize(cfg).keys.forEach(function (k) {
            (k.codes || []).forEach(function (code) { (m[code] = m[code] || []).push(k.id); });
        });
        return m;
    }

    window.KBO = {
        DEFAULT: DEFAULT, FONTS: FONTS, KEY_DEFAULTS: KEY_DEFAULTS,
        normalize: normalize, render: render, codeMap: codeMap, bounds: bounds, tiltTransform: tiltTransform, clone: clone
    };
})();
