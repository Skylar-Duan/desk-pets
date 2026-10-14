(function () {
  "use strict";
  var canvas = document.getElementById("scene-canvas"), area = document.getElementById("playground");
  var buttons = document.querySelectorAll("[data-scene].scene-button"), selected = "garden";
  var scenes = {};
  function draw() {
    var h = Math.max(1, Math.min(120, Math.floor(window.innerHeight / 5), area.clientHeight - 34));
    var density = window.devicePixelRatio || 1;
    canvas.width = Math.round(area.clientWidth * density); canvas.height = Math.round(h * density);
    canvas.style.height = h + "px"; canvas.style.opacity = "1";
    var context = canvas.getContext("2d"); context.scale(density, density);
    var scene = scenes[selected]; if (!scene) return;
    var scale = h / 96;
    for (var offset = 0; offset < area.clientWidth; offset += 384 * scale)
      scene.elements.forEach(function(s) {
        var x = offset + s.x * scale, y = s.y * scale, w = s.w * scale, sh = s.h * scale;
        context.fillStyle = s.color;
        if (s.type === "rect") context.fillRect(x, y, w, sh);
        else { context.beginPath(); if (s.type === "ellipse") context.ellipse(x+w/2,y+sh/2,w/2,sh/2,0,0,Math.PI*2);
          else {context.moveTo(x,y+sh);context.lineTo(x+w*.55,y);context.lineTo(x+w,y+sh);context.closePath();} context.fill(); }
      });
  }
  buttons.forEach(function(button) {button.addEventListener("click",function() {
    selected = button.dataset.scene; area.dataset.scene = selected;
    buttons.forEach(function(item) {item.classList.toggle("active",item===button);item.setAttribute("aria-pressed",String(item===button));}); draw();
  });});
  window.addEventListener("resize",draw);
  Promise.all(["garden","pebbles","clouds"].map(function(id) {
    return fetch("backgrounds/"+id+"/scene.json?v=20261014-r4").then(function(r){if(!r.ok)throw new Error("背景暂不可用");return r.json();}).then(function(scene){scenes[id]=scene;});
  })).then(draw).catch(function(){buttons.forEach(function(button){button.disabled=true;button.title="背景暂不可用，宠物仍可试玩";});});
})();
