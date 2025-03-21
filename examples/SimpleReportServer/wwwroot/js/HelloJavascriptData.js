window.reportIsReady = false;


(function () {
  var helloWorld = document.getElementById('HelloWorld');

  if (!helloWorld) {
    helloWorld = document.createElement('p'); // Create the element
    helloWorld.id = 'HelloWorld';
    document.body.appendChild(helloWorld); // Append it to the body
  }

  helloWorld.innerHTML = 'Hello World'; // Set text instantly
})();



//Wait 10 seconds and set reportIsReady to true

setTimeout(function () {
  window.reportIsReady = true;
}
  , 10000);
