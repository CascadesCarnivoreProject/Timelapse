using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;

namespace Carnassial.Data.Spreadsheet
{
    internal class OpenXmlStylesheet
    {
        private readonly Stylesheet stylesheet;

        public int BoldFontStyleID { get; private set; }
        public bool HasChanges { get; private set; }

        public OpenXmlStylesheet(WorkbookStylesPart styles)
        {
            this.BoldFontStyleID = -1;
            this.HasChanges = false;

            // ensure stylesheet exits
            this.stylesheet = styles.Stylesheet;
            if (this.stylesheet == null)
            {
                this.stylesheet = this.CreateDefaultStylesheet();
                this.HasChanges = true;
            }

            // find or insert a bold font
            if (this.stylesheet.Fonts == null)
            {
                this.stylesheet.Fonts = this.CreateDefaultFonts();
                this.HasChanges = true;
            }

            int boldFontIndex = -1;
            int fontIndex = 0;
            foreach (Font font in this.stylesheet.Fonts.Elements<Font>())
            {
                if (font.Bold != null)
                {
                    boldFontIndex = fontIndex;
                    break;
                }
                ++fontIndex;
            }
            if (boldFontIndex < 0)
            {
                return;
            }

            // find or insert a cell style which references the bold font
            if (this.stylesheet.CellFormats == null)
            {
                if (this.stylesheet.Fonts.Count < 2)
                {
                    throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.OpenXmlStylesheetInsufficientFonts));
                }
                this.stylesheet.CellFormats = this.CreateDefaultCellFormats();
                this.HasChanges = true;
            }
            int formatIndex = 0;
            foreach (CellFormat format in this.stylesheet.CellFormats.Elements<CellFormat>())
            {
                if (format.FontId == boldFontIndex)
                {
                    this.BoldFontStyleID = formatIndex;
                    break;
                }
                ++formatIndex;
            }
        }

        public void AcceptChanges()
        {
            this.HasChanges = false;
        }

        private CellFormats CreateDefaultCellFormats()
        {
            CellFormats cellFormats = new()
            {
                Count = UInt32Value.FromUInt32(2)
            };
            cellFormats.Append(new CellFormat()
            {
                ApplyFont = BooleanValue.FromBoolean(true),
                ApplyNumberFormat = BooleanValue.FromBoolean(true),
                FontId = UInt32Value.FromUInt32(0),
                FormatId = UInt32Value.FromUInt32(0),
                NumberFormatId = UInt32Value.FromUInt32(0)
            });
            cellFormats.Append(new CellFormat()
            {
                ApplyFont = BooleanValue.FromBoolean(true),
                ApplyNumberFormat = BooleanValue.FromBoolean(true),
                FontId = UInt32Value.FromUInt32(1),
                FormatId = UInt32Value.FromUInt32(0),
                NumberFormatId = UInt32Value.FromUInt32(0)
            });
            return cellFormats;
        }

        private Fonts CreateDefaultFonts()
        {
            Fonts fonts = new()
            {
                Count = UInt32Value.FromUInt32(2)
            };
            fonts.Append(new Font()
            {
                FontName = new FontName()
                {
                    Val = "Calibri"
                },
                FontSize = new FontSize()
                {
                    Val = DoubleValue.FromDouble(11.0)
                }
            });
            fonts.Append(new Font()
            {
                Bold = new Bold(),
                FontName = new FontName()
                {
                    Val = "Calibri"
                },
                FontSize = new FontSize()
                {
                    Val = DoubleValue.FromDouble(11.0)
                }
            });
            return fonts;
        }

        private Stylesheet CreateDefaultStylesheet()
        {
            Stylesheet stylesheet = new()
            {
                Borders = new Borders()
                {
                    Count = UInt32Value.FromUInt32(1)
                },
                CellStyleFormats = new CellStyleFormats()
                {
                    Count = UInt32Value.FromUInt32(1)
                },
                CellStyles = new CellStyles()
                {
                    Count = UInt32Value.FromUInt32(1)
                },
                DifferentialFormats = new DifferentialFormats()
                {
                    Count = UInt32Value.FromUInt32(0)
                },
                Fills = new Fills()
                {
                    Count = UInt32Value.FromUInt32(2)
                },
                NumberingFormats = new NumberingFormats()
                {
                    Count = UInt32Value.FromUInt32(0)
                },
            };
            stylesheet.Borders.Append(new Border()
            {
                BottomBorder = new BottomBorder(),
                DiagonalBorder = new DiagonalBorder(),
                LeftBorder = new LeftBorder(),
                RightBorder = new RightBorder(),
                TopBorder = new TopBorder()
            });
            stylesheet.CellFormats = this.CreateDefaultCellFormats();
            stylesheet.CellStyleFormats.Append(new CellFormat()
            {
                FontId = UInt32Value.FromUInt32(0),
                NumberFormatId = UInt32Value.FromUInt32(0)
            });
            stylesheet.CellStyles.Append(new CellStyle()
            {
                BuiltinId = UInt32Value.FromUInt32(0),
                FormatId = UInt32Value.FromUInt32(0),
                Name = "Normal"
            });
            stylesheet.Fills.Append(new Fill()
            {
                PatternFill = new PatternFill()
                {
                    PatternType = PatternValues.None
                }
            });
            stylesheet.Fills.Append(new Fill()
            {
                PatternFill = new PatternFill()
                {
                    PatternType = PatternValues.Gray125
                }
            });
            stylesheet.Fonts = this.CreateDefaultFonts();
            return stylesheet;
        }

        public Stylesheet ToStylesheet()
        {
            return this.stylesheet;
        }
    }
}
