(function () {
  "use strict";
  var canvas = document.getElementById("scene-canvas"), area = document.getElementById("playground");
  var choice = document.getElementById("scene-choice"), height = document.getElementById("scene-height");
  var transparency = document.getElementById("scene-transparency"), percent = document.getElementById("scene-percent");
  var scenes = {};
  function draw() {
    var h = Math.max(1, Math.min(Number(height.value), Math.floor(window.innerHeight / 5), area.clientHeight - 12));
    var density = window.devicePixelRatio || 1;
    canvas.width = Math.round(area.clientWidth * density); canvas.height = Math.round(h * density);
    canvas.style.height = h + "px"; canvas.style.opacity = (100 - Number(transparency.value)) / 100;
    percent.textContent = transparency.value + "%";
    var context = canvas.getContext("2d"); context.scale(density, density);
    var scene = scenes[choice.value]; if (!scene) return;
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
  [choice,height,transparency].forEach(function(input) {input.addEventListener("input",draw);});
  window.addEventListener("resize",draw);
  Promise.all(["garden","pebbles","clouds"].map(function(id) {
    return fetch("backgrounds/"+id+"/scene.json").then(function(r){if(!r.ok)throw new Error("背景暂不可用");return r.json();}).then(function(scene){scenes[id]=scene;});
  })).then(draw).catch(function(){choice.disabled=true;choice.setAttribute("title","背景暂不可用，宠物仍可试玩");});
})();
