blazorReport.notReady(); // Set initial state to "not ready"

var element = document.getElementById("HelloWorld");

// Set innerHTML to "Hello from JavaScript"
if (element) {
  element.innerHTML = "Hello from JavaScript";
}

// After 2 seconds, set the report as ready using async/await inside a self-invoking function
(async () => {
  await new Promise(resolve => setTimeout(resolve, 4000));
  blazorReport.completed(); // Mark report as ready
})();
