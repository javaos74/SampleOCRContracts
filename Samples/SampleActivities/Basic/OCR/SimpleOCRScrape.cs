using System.Collections.Generic;
using System.Diagnostics;
using UiPath.OCR.Contracts.Activities;
using UiPath.OCR.Contracts.Scrape;

namespace SampleActivities.Basic.OCR
{
    // Extend OCRScrapeBase to allow your OCR engine to display custom user controls when integrating
    // with wizards such as Screen Scraping or Template Manager.
    internal class SampleOCRScrape : OCRScrapeBase
    {
        private readonly SimpleScrapeControl _sampleScrapeControl;

        public override ScrapeEngineUsages Usage { get; } = ScrapeEngineUsages.Document | ScrapeEngineUsages.Screen;

        public SampleOCRScrape(IOCRActivity ocrEngineActivity, ScrapeEngineUsages usage) : base(ocrEngineActivity)
        {
            _sampleScrapeControl = new SimpleScrapeControl(usage);
        }

        public override ScrapeControlBase GetScrapeControl()
        {
            return _sampleScrapeControl;
        }

        public override Dictionary<string, object> GetScrapeArguments()
        {
#if DEBUG
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Application";
                eventLog.WriteEntry($"{_sampleScrapeControl.Endpoint} and {_sampleScrapeControl.Secret} called", EventLogEntryType.Information, 101, 1);
            }
#endif
            return new Dictionary<string, object>
            {
                { "apikey", _sampleScrapeControl.Secret },
                { "endpoint", _sampleScrapeControl.Endpoint}
            };
        }
    }
}
