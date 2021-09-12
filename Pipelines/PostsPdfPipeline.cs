using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Kontent.Delivery.Urls.QueryParameters;
using Kentico.Kontent.Statiq.Lumen.Models;
using Kentico.Kontent.Statiq.Lumen.Models.ViewModels;
using Kontent.Statiq;
using Newtonsoft.Json;
using Statiq.Common;
using Statiq.Core;
using Statiq.Razor;
using System.IO;
using System.Text;
using System.Linq;
using Kentico.Kontent.Delivery.Urls.QueryParameters.Filters;

namespace Kentico.Kontent.Statiq.Lumen.Pipelines
{
    public class PostsPdfPipeline : Pipeline
    {
        public PostsPdfPipeline(IDeliveryClient deliveryClient)
        {
            InputModules = new ModuleList{
                new Kontent<Article>(deliveryClient)
                    .WithQuery(new DepthParameter(1), new ContainsFilter($"elements.{Article.DownloadableCodename}", "yes")),
                new SetDestination(Config.FromDocument((doc, ctx)  => new NormalizedPath($"pdf/{doc.AsKontent<Article>().Slug}" )))
            };

            ProcessModules = new ModuleList {
                new MergeContent(new ReadFiles(patterns: "PdfPost.cshtml") ),
                new RenderRazor()
                 .WithModel(Config.FromDocument((document, context) =>
                    new PostViewModel(document.AsKontent<Article>(), new SiteMetadata{ Author = document.AsKontent<Article>().Author }))),
                new ExecuteConfig(Config.FromDocument(async (doc, ctx) =>
                {
                    var html = await doc.GetContentStringAsync();
                    // here we need to generate PDF from HTML
                    var httpClient = new System.Net.Http.HttpClient();
                    var data = new {
                        html = html,
                        apiKey = ""
                    };
                    var jsonData = JsonConvert.SerializeObject(data);
                    var httpContent = new System.Net.Http.StringContent(jsonData, Encoding.UTF8, "application/json");

                    var postResponse = await httpClient.PostAsync("https://api.html2pdf.app/v1/generate", httpContent);
                    if (!postResponse.IsSuccessStatusCode)
                    {
                        throw new System.Exception("Unable to fetch PDF");
                    }

                    var pdfBytes = await postResponse.Content.ReadAsByteArrayAsync();
                    var stream = new MemoryStream(pdfBytes);
                    var pdfDoc = ctx.CreateDocument(doc.Source, doc.Destination.ChangeExtension(".pdf"), stream, MediaTypes.Get(".pdf"));
                    return new IDocument[] {pdfDoc};
                }))
            };

            OutputModules = new ModuleList {
                new WriteFiles()
            };
        }
    }
}