# Blazor Reports

Generate PDF reports using Blazor Components. Easily create a report server or generate reports from existing projects.

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
    # Default endpoint for reports is `/reports/{componentName}`
   
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

### Configure Tailwind CSS
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
2. Add 'tailwind.css' file:
    ```css
    @tailwind base;
    @tailwind components;
    @tailwind utilities;
    ```
3. Use the following command to generate the base.css file:
    ```bash
    npx tailwindcss -i ./wwwroot/styles/tailwind/tailwind.css -o ./wwwroot/styles/base.css -m --watch
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

