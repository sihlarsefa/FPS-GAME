/* Applied by screens when ?scale=qhd */
(function () {
  var params = new URLSearchParams(location.search);
  if (params.get("scale") === "qhd") {
    var frame = document.querySelector(".frame");
    if (frame) frame.classList.add("frame--qhd");
  }
})();
