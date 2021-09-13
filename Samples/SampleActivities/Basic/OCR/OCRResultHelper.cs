using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UiPath.OCR.Contracts.DataContracts;

namespace SampleActivities.Basic.OCR
{
    internal static class OCRResultHelper
    {
        class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }
        }

        private static readonly Encoding encoding = Encoding.UTF8;
        public static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter)
                {
                    FileParameter fileToUpload = (FileParameter)param.Value;

                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                        boundary,
                        param.Key,
                        fileToUpload.FileName ?? param.Key,
                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                }
                else
                {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                        boundary,
                        param.Key,
                        param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }

        internal static string GetImageFormat( System.Drawing.Imaging.ImageFormat fmt)
        {
            if (fmt.Equals(ImageFormat.Jpeg))
                return "jpg";
            else if (fmt.Equals(ImageFormat.Tiff))
                return "tif";
            else if (fmt.Equals(ImageFormat.Bmp))
                return "bmp";
            else if (fmt.Equals(ImageFormat.Png))
                return "png";
            else if (fmt.Equals(ImageFormat.Gif))
                return "gif";
            else
                return "unknown";
        }

        internal static OCRResult FromText(string text)
        {
            return new OCRResult
            {
                Text = text,
                Words = text.Split(' ').Select((word, i) => new Word
                {
                    Text = word,
                    //PolygonPoints = new[] { new PointF((i + 1) * 100, (i + 1) * 100), new PointF((i + 1) * 200, (i + 1) * 100), new PointF((i + 1) * 100, (i + 1) * 200), new PointF((i + 1) * 200, (i + 1) * 200), },
                    Characters = word.Select(ch => new Character
                    {
                        Char = ch,
                        PolygonPoints = new[] { new PointF((i + 1) * 100, (i + 1) * 100), new PointF((i + 1) * 200, (i + 1) * 100), new PointF((i + 1) * 100, (i + 1) * 200), new PointF((i + 1) * 200, (i + 1) * 200), }
                    }).ToArray()
                }).ToArray(),
                Confidence = 0,
                SkewAngle = 0
            };
        }

        internal static OCRResult FromClovaClient(string filePath, string apiKey, string endpoint, System.Drawing.Imaging.ImageFormat format)
        {
            FileStream fs = null;
            byte[] fileData = null;
            try
            {
                fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                fileData = new byte[fs.Length];
                fs.Read(fileData, 0, fileData.Length);
                fs.Close();
                // delete file 
            }
            catch (IOException ioe)
            {
                throw new IOException(ioe.Message);
            }

            JArray jarr = new JArray();
            JObject job = new JObject();
            job.Add("version", "V2");
            job.Add("requestId", Guid.NewGuid().ToString());
            job.Add("timestamp", System.DateTime.Now.Ticks.ToString());
            JObject img = new JObject();
            JArray jtemp = new JArray();

            img.Add("name", Path.GetFileName(filePath));
            img.Add("format", GetImageFormat(format));
            jarr.Add(img);
            job.Add("images", jarr);
            Dictionary<string, object> postParameters = new Dictionary<string, object>();
            postParameters.Add("file", new FileParameter(fileData, fs.Name, "image/"+ GetImageFormat(format)));
            postParameters.Add("message", job.ToString());
            string url = endpoint;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("X-OCR-SECRET", apiKey);
            request.Method = "POST";
            var boundary = String.Format("----------{0:N}", Guid.NewGuid());
            request.ContentType = "multipart/form-data; boundary=" + boundary;

            byte[] formData = GetMultipartFormData(postParameters, boundary);
            Stream inputStream = request.GetRequestStream();
#if DEBUG
            System.IO.File.WriteAllText(@"C:\Temp\clova.req.txt", System.Text.Encoding.Default.GetString(formData));
#endif
            inputStream.Write(formData, 0, formData.Length);
            inputStream.Close();
            fileData = null;
            formData = null;
            StringBuilder sb = new StringBuilder();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            System.Console.WriteLine(response.StatusCode.ToString());
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            string text = reader.ReadToEnd();
            //인식된 문장 저장 
            //System.Console.WriteLine(text);
#if DEBUG
            System.IO.File.WriteAllText(@"C:\Temp\clova.resp.txt", text);
#endif
            //ResponseCode.Set(context, (int)response.StatusCode);
            var ocrResult = new OCRResult();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JObject respJson = JObject.Parse(text);
                JArray blocks = (JArray)respJson["images"][0]["fields"];
                ocrResult.Words = blocks.Select(p => new Word
                {
                    Text = (string)p["inferText"],
                    Confidence = Convert.ToInt32(100 * ((double)p["inferConfidence"])),
                    PolygonPoints = ((JArray)p["boundingPoly"]["vertices"]).Select(v => new PointF
                    {
                        X = (float)v["x"],
                        Y = (float)v["y"]
                    }).ToArray(),
                    Characters = ((string)p["inferText"]).Select(ch => new Character
                    {
                        Char = ch,
                    }).ToArray()
                }).ToArray();
                foreach( var blk in blocks)
                {
                    sb.Append((string)blk["inferText"]);
                    sb.Append(((Boolean)blk["lineBreak"]) ? Environment.NewLine : " ");
                }
                foreach( var word in ocrResult.Words)
                {
                    var x = word.PolygonPoints[0].X;
                    var y = word.PolygonPoints[0].Y;
                    var w = Math.Abs(word.PolygonPoints[1].X - x);
                    var y2 = word.PolygonPoints[3].Y;

                    float dx = w / word.Characters.Length;
                    float dy = Math.Abs(y2-y) / word.Characters.Length;
                    int idx = 0;
#if DEBUG
                    System.Console.WriteLine(string.Format("{0} has {1} characters", word.Text, word.Characters.Length));
#endif
                    foreach( var c in word.Characters)
                    {
                        c.PolygonPoints = new[] { new PointF(x + dx * idx, y), new PointF(x + dx * (idx+1), y), new PointF(x + dx *(idx+1), y2), new PointF(x + dx * idx, y2) };
                        c.Confidence = word.Confidence;
                        idx++;
                    }
                }
                ocrResult.Text = sb.ToString();
                ocrResult.SkewAngle = 0;
                ocrResult.Confidence = 0;
#if DEBUG
                System.IO.File.WriteAllText(@"C:\Temp\ocresult.json", Newtonsoft.Json.JsonConvert.SerializeObject(ocrResult));
                System.Console.WriteLine("full text : " + ocrResult.Text);
#endif
            }
            else
            {
            }
            stream.Close();
            response.Close();
            reader.Close();
            sb.Clear();
            sb = null;

            return ocrResult;
        }
    }
}
