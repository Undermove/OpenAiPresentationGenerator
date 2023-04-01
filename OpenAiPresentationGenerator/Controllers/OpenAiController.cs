using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Images;
using OpenAiQuickStartCSharp.Configuration;
using ShapeCrawler;
using ShapeCrawler.SlideMasters;

namespace OpenAiQuickStartCSharp.Controllers;

[ApiController]
[Route("[controller]")]
public class OpenAiController : ControllerBase
{
    private readonly OpenAiConfig _config;

    public OpenAiController(IOptions<OpenAiConfig> config)
    {
        _config = config.Value;
    }
 
    [HttpPost]
    public async Task<IActionResult> GeneratePresentation([FromBody] RequestModel request)
    {
        var apiKey = _config.ApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            return StatusCode(500, new
            {
                error = new
                {
                    message = "OpenAI API key not configured, please follow instructions in README.md"
                }
            });
        }

        var openai = new OpenAIAPI(apiKey);

        var presentationTheme = request.PresentationTheme ?? "";
        if (string.IsNullOrWhiteSpace(presentationTheme))
        {
            return StatusCode(400, new
            {
                error = new
                {
                    message = "Please enter a valid presentation theme"
                }
            });
        }

        try
        {
            // create a new presentation
            var pres = SCPresentation.Create();
            
            for (int slideNumber = 0; slideNumber < request.SlidesCount; slideNumber++)
            {
                var (header, imageResult, slideText) = await NewMethod(openai, presentationTheme, slideNumber);
                var shapeCollection = pres.Slides[slideNumber].Shapes;

                // add header
                var addedShape = shapeCollection.AddRectangle(x: 50, y: 60, w: 100, h: 70);
                addedShape.TextFrame!.Text = header.Completions[0].Text;
                
                // add picture
                var picture = pres.Slides[0].Shapes.AddRectangle(x: 50, y: 300, w: 100, h: 70);
                string url = imageResult.Data[0].Url;
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.GetAsync(url);
                Stream stream = await response.Content.ReadAsStreamAsync();
                picture.Fill.SetPicture(stream);
                
                // add text
                var text = pres.Slides[0].Shapes.AddRectangle(x: 300, y: 300, w: 100, h: 70);
                text.TextFrame!.Text = slideText.Completions[0].Text;
            }
            
            //pres.SaveAs("my_pres.pptx");
            return File(pres.BinaryData, "application/octet-stream", $"{request.PresentationTheme}.pptx");
        }
        catch (Exception error)
        {
            await Console.Error.WriteLineAsync($"Error with OpenAI API request: {error.Message}");
            return StatusCode(500, new
            {
                error = new
                {
                    message = "An error occurred during your request."
                }
            });
        }
    }

    private async Task<(CompletionResult header, ImageResult imageResult, CompletionResult slideText)> NewMethod(OpenAIAPI openai, string presentationTheme, int slideNumber)
    {
        var header = await openai.Completions.CreateCompletionAsync(
            prompt: GenerateHeaderPrompt(presentationTheme, slideNumber),
            temperature: 1
        );

        var imageResult =
            await openai.ImageGenerations.CreateImageAsync(
                $"Illustration for presentation with theme {presentationTheme} slide number {slideNumber}");

        var slideText = await openai.Completions.CreateCompletionAsync(
            prompt: GenerateTextPrompt(header.Completions[0].Text),
            temperature: 1
        );
        return (header, imageResult, slideText);
    }

    private string GenerateHeaderPrompt(string presentationTheme, int slideNumber)
    {
        var capitalized = $"{char.ToUpper(presentationTheme[0])}{presentationTheme.Substring(1).ToLower()}";
        return @$"Suggest header for slide number {slideNumber} for presentation with theme {capitalized}";
    }
    
    private string GenerateTextPrompt(string slideHeader)
    {
        return @$"Suggest text for slide with header {slideHeader}";
    }
}