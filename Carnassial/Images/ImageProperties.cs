using Carnassial.Data;
using System;
using System.Globalization;

namespace Carnassial.Images
{
    public class ImageProperties
    {
        public double Coloration { get; private set; }
        public double Luminosity { get; private set; }
        public MetadataReadResults MetadataResult { get; set; }

        public ImageProperties(MetadataReadResults metadataResult)
        {
            this.MetadataResult = metadataResult;
            this.Coloration = -1.0;
            this.Luminosity = -1.0;
        }

        public ImageProperties(double luminosity, double coloration)
        {
            this.MetadataResult = MetadataReadResults.None;
            this.Coloration = coloration;
            this.Luminosity = luminosity;
        }

        public bool HasColorationAndLuminosity
        {
            get { return (this.Coloration >= 0.0) && (this.Luminosity >= 0.0); }
        }

        public FileClassification EvaluateNewClassification(double darkLuminosityThreshold)
        {
            if (this.HasColorationAndLuminosity == false)
            {
                return this.MetadataResult switch
                {
                    MetadataReadResults.Failed => FileClassification.Corrupt,
                    MetadataReadResults.None => FileClassification.NoLongerAvailable,
                    _ => throw new NotSupportedException("Unhandled metadata result " + this.MetadataResult.ToString()),
                };
            }

            // a truly greyscale pixel has r = g = b but allow for some color cast
            // Color cast in greyscale can result from
            // - pick up of manufacturer logos or info bar color not excluded in the skip above
            // - jpeg quantization
            // - not quite greyscale output from the camera
            if (this.Coloration < Constant.Images.GreyscaleColorationThreshold)
            {
                if (this.Luminosity < darkLuminosityThreshold)
                {
                    // color images are never considered to be dark
                    return FileClassification.Dark;
                }
                return FileClassification.Greyscale;
            }
            return FileClassification.Color;
        }

        public string GetClassificationDescription()
        {
            if (this.HasColorationAndLuminosity == false)
            {
                return "File could not be loaded because it is missing or corrupt.";
            }

            if (this.Coloration >= Constant.Images.GreyscaleColorationThreshold)
            {
                return "File is in color and therefore not dark.";
            }
            return String.Format(CultureInfo.CurrentCulture, "Normalized brightness is {0:P1}.", this.Luminosity);
        }
    }
}
