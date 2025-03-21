// Set report status to "processing"
blazorReport.ready();

// Get the element with id "HelloWorld"
var element = document.getElementById("HelloWorld");

// Set innerHTML to "Hello from JavaScript"
if (element) {
  element.innerHTML = "Hello from JavaScript";
}

// Wait 10 seconds, then mark report as ready
setTimeout(() => {
  blazorReport.completed();
}, 2_000);
