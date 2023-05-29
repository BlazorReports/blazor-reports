# Blazor Reports

Generate PDF reports using Blazor Components.

## Basic usage for web applications

1. Install the Blazor Reports NuGet package:
    ```bash
    dotnet add package BlazorReports
    ```
2. Configure Blazor Reports and map the component:
    ```c#
    var builder = WebApplication.CreateSlimBuilder(args);
    
    builder.Services.AddBlazorReports(); // Configure BlazorReports
    
    var app = builder.Build();
    
    app.MapBlazorReport<MyBlazorComponent>(); // Map Blazor Component
    app.MapBlazorReport<OtherBlazorComponent, MyDataDto>(); // Map Blazor Component and receive data
    
    app.Run();
    ``` 
3. Send HTTP POST request to the Blazor Reports endpoint:
    ```http
    ## Default endpoint for reports is `/reports/{componentName}`
   
    POST /reports/MyBlazorComponent
   
    POST /reports/OtherBlazorComponent
    Content-Type: application/json
    {
      "text": "Hello World!"
    }
    ```
4. Get back PDF Report. 

Sample Blazor Components
```c#
// MyBlazorComponent: Basic component
<h3>Hello World!</h3>

@code {

}
```

```c#
// OtherBlazorComponent: Component that receives data
<h3>@Data?.Text</h3>

@code {
    [Parameter]
    public MyDataDto? Data { get; set; }
}
```

## Configure Tailwind CSS
1. Add 'tailwind.config.js' file to the root of your project:
    ```js
    /** @type {import('tailwindcss').Config} */
    module.exports = {
    content: ['./**/*.{razor,html}'],
    theme: {
    extend: {},
    },
    plugins: [],
    }
    ```
2. Add 'tailwind.css' file in 'wwwroot/styles' folder:
    ```css
    @tailwind base;
    @tailwind components;
    @tailwind utilities;
    ```
3. Use the following command to generate the base.css file:
    ```bash
    npx tailwindcss -i ./wwwroot/styles/tailwind.css -o ./wwwroot/css/base.css -m --watch
    ```
4. Configure BaseStyles in Program.cs:
    ```c#
    var builder = WebApplication.CreateSlimBuilder(args);
    
    builder.Services.AddBlazorReports(options =>
    {
      options.BaseStylesPath = "wwwroot/styles/base.css";
    });
    
    var app = builder.Build();
    
    app.MapBlazorReport<MyBlazorComponent>();
    
    app.Run();
    ```
