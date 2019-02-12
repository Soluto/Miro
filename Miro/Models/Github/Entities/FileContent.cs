using System;
using System.Linq;
using System.Text;
using Miro.Services.Logger;
using Miro.Services.Utils;
using Serilog;

namespace Miro.Models.Github.Entities
{

    public class FileContent
    {

        public string Encoding { get; set; }
        public string Content { get; set; }
        public string Sha { get; set; }
        public string Name { get; set; }

    }

    public static class FileContentExt
    {
        private static readonly ILogger logger = Log.ForContext<FileContent>();

        public static T DecodeContent<T>(this FileContent fileContent, string format = "yaml")
        {
            var content = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent.Content));
            switch (format)
            {
                case "yaml":
                    try
                    {
                        return content.Split("\n")
                      .Where(x => !x.Contains("#") && x.Contains(":"))
                      .ToDictionary(x =>
                      {
                          var keysValues = x.Split(":");
                          return keysValues[0].Trim();

                      }, x =>
                      {
                          var keysValues = x.Split(":");
                          return keysValues[1].Trim();
                      }).ToObject<T>();
                    }
                    catch (Exception e)
                    {
                        logger.WithExtraData(new {fileContent, content}).Error(e, "Could not parse into yaml format, returning default object");
                        return default(T);
                    }


                default:
                    throw new Exception("I dont know how to decode this");
            }

        }
    }
}