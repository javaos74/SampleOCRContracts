using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using UiPath.OCR.Contracts;
using UiPath.OCR.Contracts.Activities;
using UiPath.OCR.Contracts.DataContracts;

namespace SampleActivities.Basic.OCR
{
    [DisplayName("Clova OCR Engine")]
    public class SimpleOCREngine : OCRCodeActivity
    {
        [Category("Input")]
        [Browsable(true)]
        public override InArgument<Image> Image { get => base.Image; set => base.Image = value; }

        [Category("Output")]
        [Browsable(true)]
        public override OutArgument<string> Text { get => base.Text; set => base.Text = value; }

        [Category("Input")]
        [RequiredArgument]
        public InArgument<string> Endpoint { get; set; }

        [Category("Input")]
        [RequiredArgument]
        public InArgument<string> ApiKey { get; set; }

        private string filepath;


        public override Task<OCRResult> PerformOCRAsync(Image image, Dictionary<string, object> options, CancellationToken ct)
        {
            //string customInput = options[nameof(CustomInput)] as string;
            string text = $"Text from {nameof(SimpleOCREngine)} with custom input: Charles Kim ";
            filepath = System.IO.Path.Combine(@"C:\Temp", "clova.png");
            string fileFormat = image.RawFormat.ToString();
            image.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
#if DEBUG
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Application";
                foreach( var k in options.Keys)
                    eventLog.WriteEntry($"Entry Key:{k}  and Value: {options[k].ToString()}", EventLogEntryType.Information, 101, 1);
            }
            System.Console.WriteLine("temp file path " + filepath);
#endif
            return Task.FromResult(OCRResultHelper.FromClovaClient(filepath, options["apikey"].ToString(), options["endpoint"].ToString(), System.Drawing.Imaging.ImageFormat.Png));
            //return Task.FromResult(OCRResultHelper.FromText(text));
        }

        protected override void OnSuccess(CodeActivityContext context, OCRResult result)
        {
            //base.Text.Set(context, result.Text);
            //delete temp file 
            System.IO.File.Delete(filepath);
;        }

        protected override Dictionary<string, object> BeforeExecute(CodeActivityContext context)
        {
            return new Dictionary<string, object>
            {
                { "endpoint",  Endpoint.Get(context) },
                { "apikey", ApiKey.Get(context) }
            };
        }
    }
}
