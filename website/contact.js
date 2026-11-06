/* Prunel website: shows the contact e-mail address only when a visitor clicks the
   "Show the e-mail address" button. The address is never written as plain text in the
   HTML pages, so most spam robots that read web pages cannot collect it.
   To change the address, change the two parts below. */
(function () {
  var name = ["p", "r", "u", "n", "e", "l"].join("");
  var domain = ["kopol", "xyz"].join(".");

  var button = document.getElementById("show-email");
  var target = document.getElementById("email");
  if (!button || !target) {
    return;
  }

  button.hidden = false;
  button.addEventListener("click", function () {
    var address = name + "@" + domain;
    var link = document.createElement("a");
    link.className = "button button-second email-link";
    link.href = "mailto:" + address;
    link.textContent = address;
    target.appendChild(link);
    button.hidden = true;
    link.focus();
  });
})();
