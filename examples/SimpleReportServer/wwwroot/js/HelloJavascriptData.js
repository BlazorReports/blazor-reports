
var element = document.getElementById("HelloWorld");

// Set innerHTML to "Hello from JavaScript"
if (element) {
  element.innerHTML = "Hello from JavaScript";
}

//after 5 seconds complete the report with blazorReport.completed(); // Mark report as ready

setTimeout(() => {
  blazorReport.completed(); // Mark report as ready
}, 5_000)

