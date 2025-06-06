# Blazor Reports

Generate PDF reports using Blazor Components. Easily create a report server or generate reports from existing projects.

## Requirements

* .NET 8.0 or later for report server
* .NET 6.0 or later for Blazor Components shared library
* Chrome, Chromium, or Edge browser for report generation

## Basic usage for report server

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
    POST /MyBlazorComponent
   
    POST /OtherBlazorComponent
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
    public required MyDataDto Data { get; set; }
}
```

## Advanced usage

### Add Base styles
1. Configure base styles file in options for AddBlazorReports:
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
2. Configured components will now have the base styles applied.

### Configure Tailwind CSS V4
1. Add Tailwind to the root of your repository:
    ```bash
    bun install tailwindcss @tailwindcss/cli
    ```
2. Add 'wwwroot/tailwindcss/input.css' file to your project:
    ```css
    @import "tailwindcss";
    ```
3. Add a watch and build script to your package.json for convenience:
    ```json
    {
      "scripts": {
        "tailwind-watch": "tailwindcss -i ./path_to_your_project/wwwroot/tailwindcss/input.css -o ./path_to_your_project/wwwroot/styles/base.css -m --watch",
        "tailwind-build": "tailwindcss -i ./path_to_your_project/wwwroot/tailwindcss/input.css -o ./path_to_your_project/wwwroot/styles/base.css -m"
      },
      "dependencies": {
        "@tailwindcss/cli": "^4.0.14",
        "tailwindcss": "^4.0.14"
      }
    }
    ```
4. Use the following command to generate the base.css file:
    ```bash
    bun run tailwind-build # To build once
    bun run tailwind-watch # To watch for changes
    ```
5. Alternatively, use the tailwind cli to generate the base.css file:
    ```bash
    bunx @tailwindcss/cli -i ./path_to_your_project/wwwroot/tailwindcss/input.css -o ./path_to_your_project/wwwroot/styles/base.css -m --watch
    ```
6. Finally, configure BaseStyles in the Program.cs:
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
   
### Configure assets
1. Configure assets in Program.cs:
    ```c#
    var builder = WebApplication.CreateSlimBuilder(args);
    
    builder.Services.AddBlazorReports(options =>
    {
      options.AssetsPath = "wwwroot/assets";
    });
    
    var app = builder.Build();
    
    app.MapBlazorReport<MyBlazorComponent>();
    
    app.Run();
    ```
2. Add inheritance for BlazorReportsComponentBase:
    ```c#
    @inherits BlazorReports.Components.BlazorReportsBase
    
    <img src="@GlobalAssets.GetValueOrDefault("logo-salud.png")"/>
    
    @code {
    
    }
    ```
3. All files will be available as base64 strings in the 'GlobalAssets' dictionary.
   
### OpenAPI, Swagger, Authentication, Authorization, Validation, etc.
Blazor reports utilizes Minimal APIs under the hood and can be configured as any other Minimal API project.

For example, to add OpenAPI and Swagger:
1. Add OpenAPI and Swagger NuGet packages.
2. Configure OpenAPI and Swagger:
    ```c#
    var builder = WebApplication.CreateSlimBuilder(args);
    
    builder.Services.AddBlazorReports();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    var app = builder.Build();
    
    app.UseSwagger();
    app.UseSwaggerUI();
    
    app.MapBlazorReport<MyComponent, MyComponentData>();
    app.MapBlazorReport<MyOtherComponent>();
    
    app.Run();
    ```
3. Open Swagger UI at '/swagger' endpoint.
4. You can see each report endpoint configured with expected data model.

