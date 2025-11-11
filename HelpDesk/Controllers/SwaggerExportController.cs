using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;

[ApiController]
[Route("api/[controller]")]
public class SwaggerExportController : ControllerBase
{
    private readonly ISwaggerProvider _swaggerProvider;

    public SwaggerExportController(ISwaggerProvider swaggerProvider)
    {
        _swaggerProvider = swaggerProvider;
    }

    [HttpGet("yaml")]
    public IActionResult GetYaml()
    {
        var swagger = _swaggerProvider.GetSwagger("v1");
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        var yamlWriter = new OpenApiYamlWriter(writer);
        swagger.SerializeAsV3(yamlWriter);
        writer.Flush();
        stream.Position = 0;
        return File(stream.ToArray(), "application/yaml", "swagger.yaml");
    }
}
