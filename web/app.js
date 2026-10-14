/* 桌面小伙伴 · 网页版演示引擎（与桌面端同一套素材与状态语义的简化版） */
(function () {
  "use strict";

  var PETS = ["ragdoll", "golden", "nuomi"];
  var reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var playground = document.getElementById("playground");

  function loadPet(id) {
    return fetch("assets/" + id + "/pet.json")
      .then(function (r) { return r.json(); })
      .then(function (def) { def.id = id; return def; });
  }

  // ---------------------------------------------------------- 互动区宠物
  function WebPet(def, fracX, playful) {
    this.def = def;
    this.playful = playful;
    this.scale = def.scale || 3;
    this.w = def.frameSize[0] * this.scale;
    this.h = def.frameSize[1] * this.scale;
    this.x = (playground.clientWidth - this.w) * fracX;
    this.y = this.groundY();
    this.vx = 0; this.vy = 0;
    this.facing = 1;
    this.state = "idle";
    this.stateT = 0; this.stateDur = 2;
    this.animT = 0;
    this.chasing = false;
    this.buddy = null;
    this.dragging = false;

    this.el = document.createElement("div");
    this.el.className = "pet";
    this.el.classList.add(def.pixelArt === false ? "modern" : "pixel");
    this.el.setAttribute("aria-label", def.name + "，可拖动");
    this.el.style.width = this.w + "px";
    this.el.style.height = this.h + "px";
    this.inner = document.createElement("div");
    this.inner.className = "pet-inner";
    this.el.appendChild(this.inner);
    playground.appendChild(this.el);
    this.bindPointer();
    this.enter("idle");
  }

  WebPet.prototype.groundY = function () {
    return playground.clientHeight - this.h - 12;
  };

  WebPet.prototype.anim = function () {
    var st = this.def.states[this.state] || this.def.states.idle;
    return st;
  };

  WebPet.prototype.enter = function (s) {
    if (!this.def.states[s]) s = fallback(this.def, s);
    this.state = s;
    this.stateT = 0;
    this.animT = 0;
    var r = Math.random;
    this.stateDur =
      s === "idle" ? 2 + r() * 3 :
      s === "walk" ? 3 + r() * 4 :
      s === "run" ? 1.5 + r() * 1.5 :
      s === "sit" ? 4 + r() * 4 :
      s === "sleep" ? 6 + r() * 8 :
      s === "happy" ? 1.4 : 1e9;
    if (s === "walk") { this.vx = 42 * this.pickDir(); this.facing = this.vx >= 0 ? 1 : -1; }
    else if (s === "run" && !this.chasing) { this.vx = 150 * this.pickDir(); this.facing = this.vx >= 0 ? 1 : -1; }
    else if (s !== "fall") this.vx = 0;
  };

  WebPet.prototype.pickDir = function () {
    if (this.x < 60) return 1;
    if (this.x + this.w > playground.clientWidth - 60) return -1;
    return Math.random() < 0.5 ? 1 : -1;
  };

  WebPet.prototype.tick = function (dt) {
    this.stateT += dt;
    this.animT += dt;
    var ground = this.groundY();

    if (!this.dragging && this.state !== "fall" && this.y < ground - 2) {
      this.vy = 0; this.enter("fall");
    }

    if (this.state === "walk" || this.state === "run") {
      if (this.chasing && this.buddy) {
        var target = this.buddy.x + this.buddy.w / 2;
        var me = this.x + this.w / 2;
        var dir = target > me ? 1 : -1;
        this.vx = 150 * dir; this.facing = dir;
        if (Math.abs(target - me) < 110 || this.stateT > 6) {
          this.chasing = false;
          this.enter("happy");
          if (Math.random() < 0.5 && ["idle", "walk", "sit"].indexOf(this.buddy.state) >= 0)
            this.buddy.enter("happy");
        }
      }
      this.x += this.vx * dt;
      if (this.x < 0) { this.x = 0; this.vx = Math.abs(this.vx); this.facing = 1; }
      var maxX = playground.clientWidth - this.w;
      if (this.x > maxX) { this.x = maxX; this.vx = -Math.abs(this.vx); this.facing = -1; }
      this.y = ground;
    } else if (this.state === "fall") {
      this.vy += 2500 * dt;
      this.x += this.vx * dt;
      this.y += this.vy * dt;
      if (this.x < 0) { this.x = 0; this.vx = -this.vx * 0.5; }
      var mX = playground.clientWidth - this.w;
      if (this.x > mX) { this.x = mX; this.vx = -this.vx * 0.5; }
      if (this.vx !== 0) this.facing = this.vx >= 0 ? 1 : -1;
      if (this.y >= ground) {
        this.y = ground;
        if (Math.abs(this.vy) > 450) { this.vy = -this.vy * 0.35; this.vx *= 0.6; }
        else { this.vy = 0; this.vx = 0; this.enter("idle"); }
      }
    }

    if (this.stateT >= this.stateDur && this.state !== "fall" && !this.dragging)
      this.pickNext();

    this.render();
  };

  WebPet.prototype.pickNext = function () {
    if (this.playful && this.buddy && Math.random() < 0.25 &&
        Math.abs(this.buddy.x - this.x) > playground.clientWidth * 0.35) {
      this.chasing = true;
      this.enter("run");
      this.stateDur = 6;
      return;
    }
    var r = Math.random();
    if (r < 0.28) this.enter("idle");
    else if (r < 0.62) this.enter("walk");
    else if (r < 0.72) this.enter("run");
    else if (r < 0.90) this.enter("sit");
    else this.enter("sleep");
  };

  WebPet.prototype.render = function () {
    var st = this.anim();
    var n = st.frames;
    var idx = reducedMotion ? 0 : st.loop ? Math.floor(this.animT * st.fps) % n : Math.min(Math.floor(this.animT * st.fps), n - 1);
    var fw = this.def.frameSize[0], fh = this.def.frameSize[1];
    this.el.style.transform = "translate(" + Math.round(this.x) + "px," + Math.round(this.y) + "px)";
    this.inner.style.transform = this.facing < 0 && !st.leftFile ? "scaleX(-1)" : "";
    this.inner.style.backgroundImage = "url(assets/" + this.def.id + "/" + (this.facing < 0 && st.leftFile ? st.leftFile : st.file) + ")";
    this.inner.style.backgroundSize = (fw * n * this.scale) + "px " + (fh * this.scale) + "px";
    this.inner.style.backgroundPosition = (-idx * fw * this.scale) + "px 0";
  };

  WebPet.prototype.bindPointer = function () {
    var self = this;
    var downX = 0, downY = 0, downT = 0, moved = false, grabX = 0, grabY = 0;
    var samples = [];
    this.el.addEventListener("pointerdown", function (e) {
      e.preventDefault();
      self.el.setPointerCapture(e.pointerId);
      self.dragging = true;
      self.el.classList.add("grabbed");
      self.enter("drag");
      downX = e.clientX; downY = e.clientY; downT = performance.now();
      var rect = playground.getBoundingClientRect();
      grabX = e.clientX - rect.left - self.x; grabY = e.clientY - rect.top - self.y;
      moved = false;
      samples = [];
    });
    this.el.addEventListener("pointermove", function (e) {
      if (!self.dragging) return;
      var rect = playground.getBoundingClientRect();
      if (Math.abs(e.clientX - downX) + Math.abs(e.clientY - downY) > 6) moved = true;
      self.x = e.clientX - rect.left - grabX;
      self.y = e.clientY - rect.top - grabY;
      samples.push([performance.now(), e.clientX, e.clientY]);
      if (samples.length > 10) samples.shift();
      self.render();
    });
    function up(e) {
      if (!self.dragging) return;
      self.dragging = false;
      self.el.classList.remove("grabbed");
      if (self.el.hasPointerCapture(e.pointerId)) self.el.releasePointerCapture(e.pointerId);
      var isClick = e.type === "pointerup" && !moved && performance.now() - downT < 400;
      if (isClick) { self.enter("happy"); return; }
      var vx = 0, vy = 0;
      if (samples.length >= 2) {
        var last = samples[samples.length - 1], first = samples[0];
        for (var i = samples.length - 2; i >= 0; i--) {
          first = samples[i];
          if (last[0] - first[0] > 100) break;
        }
        var span = (last[0] - first[0]) / 1000;
        if (span > 0.01) { vx = (last[1] - first[1]) / span; vy = (last[2] - first[2]) / span; }
      }
      self.vx = Math.max(-900, Math.min(900, vx));
      self.vy = Math.max(-1500, Math.min(1500, vy));
      self.enter("fall");
    }
    this.el.addEventListener("pointerup", up);
    this.el.addEventListener("pointercancel", up);
  };

  function fallback(def, s) {
    var chain = { run: "walk", walk: "idle", sit: "idle", sleep: "sit", drag: "fall", fall: "drag", happy: "idle" };
    var seen = {};
    while (s && !def.states[s] && !seen[s]) { seen[s] = 1; s = chain[s]; }
    return def.states[s] ? s : "idle";
  }

  // ---------------------------------------------------------- 图鉴
  function buildGallery(def) {
    var row = document.getElementById("gallery-" + def.id);
    if (!row) return;
    var strip = row.querySelector(".strip");
    var scale = def.scale || 1;
    var labels = {idle:"发呆",walk:"散步",run:"奔跑",sit:"坐下",sleep:"睡觉",drag:"提起",fall:"下落",happy:"开心"};
    Object.keys(def.states).forEach(function (name) {
      var st = def.states[name];
      var cell = document.createElement("div");
      cell.className = "cell";
      var a = document.createElement("div");
      a.className = "anim";
      if (def.pixelArt === false) a.classList.add("modern");
      var fw = def.frameSize[0], fh = def.frameSize[1];
      a.style.width = fw * scale + "px";
      a.style.height = fh * scale + "px";
      a.style.backgroundImage = "url(assets/" + def.id + "/" + st.file + ")";
      a.style.backgroundSize = (fw * st.frames * scale) + "px " + (fh * scale) + "px";
      var label = document.createElement("div");
      label.className = "label";
      label.textContent = labels[name] || name;
      cell.appendChild(a);
      cell.appendChild(label);
      strip.appendChild(cell);
      var t = 0;
      setInterval(function () {
        t += 1 / st.fps > 0.5 ? 0.5 : 1 / st.fps;
        if (document.hidden) return;
        var idx = reducedMotion ? 0 : Math.floor(t * st.fps) % st.frames;
        a.style.backgroundPosition = (-idx * fw * scale) + "px 0";
      }, Math.max(1000 / st.fps, 60));
    });
  }

  // ---------------------------------------------------------- 启动
  Promise.all(PETS.map(loadPet)).then(function (defs) {
    var pets = defs.map(function (def, i) {
      return new WebPet(def, defs.length === 1 ? 0.5 : 0.1 + 0.8 * i / (defs.length - 1), def.playful);
    });
    if (pets.length > 1) {
      pets.forEach(function (p, i) { p.buddy = pets[(i + 1) % pets.length]; });
    }
    var last = performance.now();
    setInterval(function () {
      if (document.hidden) return;
      var now = performance.now();
      var dt = Math.min((now - last) / 1000, 0.1);
      last = now;
      pets.forEach(function (p) { if (reducedMotion && !p.dragging && p.state !== "fall") p.render(); else p.tick(dt); });
    }, 33);
    defs.forEach(buildGallery);
    var selectors = document.getElementById("pet-selectors");
    pets.forEach(function(p) {
      var group = document.createElement("label");
      var check = document.createElement("input"); check.type = "checkbox"; check.checked = true;
      check.setAttribute("aria-label", "展示" + p.def.name);
      check.addEventListener("change", function() { p.el.hidden = !check.checked; });
      group.appendChild(check); group.appendChild(document.createTextNode(" " + p.def.name + " "));
      var size = document.createElement("input"); size.type = "number"; size.min = 50; size.max = 200; size.step = 10; size.value = 100;
      size.setAttribute("aria-label", p.def.name + "演示大小");
      size.addEventListener("change", function() {
        var percent = Math.max(50, Math.min(200, Number(size.value) || 100)); size.value = percent;
        var foot = p.y + p.h, center = p.x + p.w / 2;
        p.scale = (p.def.scale || 1) * percent / 100;
        p.w = p.def.frameSize[0] * p.scale; p.h = p.def.frameSize[1] * p.scale;
        p.x = Math.max(0, Math.min(playground.clientWidth - p.w, center - p.w / 2)); p.y = foot - p.h;
        p.el.style.width = p.w + "px"; p.el.style.height = p.h + "px"; p.render();
      });
      group.appendChild(size); group.appendChild(document.createTextNode("%")); selectors.appendChild(group);
    });
    window.addEventListener("resize", function() { pets.forEach(function(p) { p.x = Math.max(0,Math.min(p.x,playground.clientWidth-p.w)); if (!p.dragging && p.state !== "fall") p.y = p.groundY(); p.render(); }); });
    window.DeskPetsDemo = {pets:pets, defs:defs};
  }).catch(function (err) {
    playground.innerHTML = '<p style="padding:20px">素材加载失败：' + err + "</p>";
  });
})();
