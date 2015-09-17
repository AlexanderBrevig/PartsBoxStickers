using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LazyCLI;
using Newtonsoft.Json;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PartsBoxStickers
{

    class Program
    {
        static class Config
        {
            public static bool AllStorage { get; set; }
            public static string Storage { get; set; }
            public static bool AllParts { get; set; }
            public static string Part { get; set; }

            public static float Width { get; set; }
            public static float Height { get; set; }
            public static float Margin { get; set; }


            [LazyCLI("-h")]
            public static void Help1() { Help(); }
            [LazyCLI("-H")]
            public static void Help2() { Help(); }
            [LazyCLI("-help")]
            public static void Help3() { Help(); }
            [LazyCLI("-?")]
            public static void Help4() { Help(); }

            public static void Help()
            {
                Console.WriteLine("\r\nPartsBoxStickers.exe usage:");
                Console.WriteLine(" -storages\r\n\tprint all storage labels");
                Console.WriteLine(" -storage hardware1,hardware2\r\n\tprint storage label for hardware1 and hardware2 location");
                Console.WriteLine(" -parts\r\n\tprint all part labels");
                Console.WriteLine(" -part partX,partY\r\n\tprint part label for partX and partY");
                Console.WriteLine(" -width 5\r\n\tsticker width is 5 cm");
                Console.WriteLine(" -height 2.5\r\n\tsticker height is 2.5 cm");
                Console.WriteLine(" -margin 2.54\r\n\tmargin is 2.54 cm");
            }

            [LazyCLI("-storages")]
            public static void EnableAllStorageHelper()
            {
                EnableAllStorage(null);
            }

            [LazyCLI("-storage")]
            public static void EnableAllStorage(string stor)
            {
                if (stor == null) {
                    AllStorage = true;
                    Console.WriteLine("> Print all storage labels");
                } else {
                    AllStorage = false;
                    Storage = stor;
                    Console.WriteLine("> Print storages '{0}'", stor);
                }
            }
            [LazyCLI("-parts")]
            public static void EnableAllPartsHelper()
            {
                EnableAllParts(null);
            }

            [LazyCLI("-part")]
            public static void EnableAllParts(string part)
            {
                if (part == null) {
                    AllParts = true;
                    Console.WriteLine("> Print all part labels");
                } else {
                    AllParts = false;
                    Part = part;
                    Console.WriteLine("> Print parts '{0}'", part);
                }
            }

            [LazyCLI("-width")]
            public static void SetWidth(string width)
            {
                float w = 0;
                if (float.TryParse(width, out w)) {
                    Width = w;
                }
            }

            [LazyCLI("-height")]
            public static void SetHeight(string height)
            {
                float h = 0;
                if (float.TryParse(height, out h)) {
                    Height = h;
                }
            }

            [LazyCLI("-margin")]
            public static void SetMargin(string margin)
            {
                float m = 0;
                if (float.TryParse(margin, out m)) {
                    Margin = m;
                }
            }
        }

        static void Main(string[] args)
        {
            Config.Help();
            bool didSendFile = File.Exists(args[0]);
            if (!didSendFile && !File.Exists("partsbox-data.json")) {
                Console.WriteLine("\r\nYou must supply the file as first argument, typically partsbox-data.json");
                Console.ReadKey();
                return;
            }

            Config.Width = 5;
            Config.Height = 2;
            Config.AllStorage = true;
            Config.AllParts = true;
            Config.Margin = 2.54f;

            CLI.HandleArgs(args);

            try {
                string json = File.ReadAllText(didSendFile ? args[0] : "partsbox-data.json");
                PartsBox box = JsonConvert.DeserializeObject<PartsBox>(json);

                currentPage = -1;
                currentStickerCount = 0;

                GeneratePDF(box);
            } catch (Exception e) {
                Console.WriteLine("An error occurred!");
                Console.WriteLine(e.ToString());
            }
        }

        private static void GeneratePDF(PartsBox box)
        {
            PdfDocument document = new PdfDocument();


            if (!Config.AllStorage && !string.IsNullOrEmpty(Config.Storage)) {
                var storages = Config.Storage.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                DoStickers<Storage>(document, box.storage.Where(p => storages.Contains(p.name)).ToList(), StorageSticker);
            } else if (Config.AllStorage) {
                DoStickers<Storage>(document, box.storage, StorageSticker);
            }

            if (!Config.AllParts && !string.IsNullOrEmpty(Config.Part)) {
                var parts = Config.Part.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                DoStickers<Part>(document, box.parts.Where(p => parts.Contains(p.name)).ToList(), PartSticker);
            } else if (Config.AllParts) {
                currentPage = -1; //force new page
                DoStickers<Part>(document, box.parts, PartSticker);
            }

            string filename = "PartsBox_" + DateTime.Now.ToLongTimeString().Replace(":", "-").Replace(".", "").Replace(" ", "-") + ".pdf";
            document.Save(filename);
            Process.Start(filename);
        }

        private static int currentPage;
        private static int currentSticker;
        private static int currentStickerCount;
        private static int stickersPerLine;
        private static int linesPerPage;
        private static PdfPage page;
        private static XGraphics gfx;
        private static void DoStickers<T>(PdfDocument doc, List<T> items, Action<XGraphics, T, XUnit, XUnit> act)
        {
            float spacing = 0.5f;
            var pageSize = PageSize.A4;
            foreach (var item in items) {
                if (currentPage == -1 
                    || (currentStickerCount / stickersPerLine / linesPerPage > currentPage
                        && currentSticker % stickersPerLine == 0)) {
                    page = doc.AddPage();
                    page.TrimMargins.All = XUnit.FromCentimeter(Config.Margin); //TODO: add margin to config?
                    page.Size = pageSize;
                    gfx = XGraphics.FromPdfPage(page);
                    currentPage = currentPage < 0 ? 0 : currentStickerCount / stickersPerLine / linesPerPage;
                    currentSticker = 0;
                }
                stickersPerLine = (int)(page.Width.Centimeter / (Config.Width + spacing));
                linesPerPage = (int)(page.Height.Centimeter / (Config.Height + spacing));
                var x = XUnit.FromCentimeter((currentSticker % stickersPerLine) * (Config.Width + spacing));
                var y = XUnit.FromCentimeter((Config.Height + spacing) * (currentSticker / stickersPerLine));

                var boxpen = new XPen(XColors.Black, 1);
                gfx.DrawRectangle(boxpen,
                    XBrushes.Transparent,
                    new XRect(x, y, XUnit.FromCentimeter(Config.Width), XUnit.FromCentimeter(Config.Height)));

                act(gfx, item, x, y);
                currentSticker++;
                currentStickerCount++;
            }
        }

        private static void StorageSticker(XGraphics gfx, Storage item, XUnit x, XUnit y)
        {
            int marg = 4;
            var boxpen = new XPen(XColors.Black, 1);
            gfx.DrawRectangle(boxpen,
                XBrushes.Transparent,
                new XRect(x + marg, y + marg, XUnit.FromCentimeter(Config.Width) - (2 * marg), XUnit.FromCentimeter(Config.Height) - (2 * marg)));

            XFont font = new XFont("Consolas", 25, XFontStyle.Bold);
            while (gfx.MeasureString(item.name, font).Width > XUnit.FromCentimeter(Config.Width - 1)) {
                font = new XFont("Consolas", font.Size - 0.1, XFontStyle.Bold);
            }
            gfx.DrawString(item.name,
                font,
                XBrushes.Black,
                new XRect(x, y, XUnit.FromCentimeter(Config.Width), XUnit.FromCentimeter(Config.Height)),
                XStringFormats.Center);
            //var items = box.parts.Where(p => p.stock.Count(s => s.storage == item.id) > 0);
            font = new XFont("Consolas", 8, XFontStyle.Regular);
            while (gfx.MeasureString(item.id, font).Width > XUnit.FromCentimeter(Config.Width - 0.2) - 2 * marg) {
                font = new XFont("Consolas", font.Size - 0.1, XFontStyle.Regular);
            }
            gfx.DrawString(item.id,
                font,
                XBrushes.Gray,
                new XRect(x, y - 5, XUnit.FromCentimeter(Config.Width), XUnit.FromCentimeter(Config.Height)),
                XStringFormats.BottomCenter);
        }

        private static void PartSticker(XGraphics gfx, Part item, XUnit x, XUnit y)
        {
            XFont font = new XFont("Consolas", 22, XFontStyle.Bold);
            while (gfx.MeasureString(item.name, font).Width > XUnit.FromCentimeter(Config.Width - 1)) {
                font = new XFont("Consolas", font.Size - 0.1, XFontStyle.Bold);
            }
            gfx.DrawString(item.name,
                font,
                XBrushes.Black,
                new XRect(x, y, XUnit.FromCentimeter(Config.Width), XUnit.FromCentimeter(Config.Height)),
                XStringFormats.TopCenter);


            font = new XFont("Consolas", 16, XFontStyle.Bold);
            var info = item.description;
            while (gfx.MeasureString(info, font).Width > XUnit.FromCentimeter(Config.Width - 1)) {
                font = new XFont("Consolas", font.Size - 0.1, XFontStyle.Bold);
            }
            gfx.DrawString(info,
                font,
                XBrushes.Black,
                new XRect(x, y, XUnit.FromCentimeter(Config.Width), XUnit.FromCentimeter(Config.Height)),
                XStringFormats.Center);

            var footprintFont = new XFont("Consolas", 12, XFontStyle.Italic);
            gfx.DrawString(item.footprint,
                footprintFont,
                XBrushes.Black,
                new XRect(x, y + gfx.MeasureString(info, font).Height + 5, XUnit.FromCentimeter(Config.Width), XUnit.FromCentimeter(Config.Height)),
                XStringFormats.Center);


            //var items = box.parts.Where(p => p.stock.Count(s => s.storage == item.id) > 0);
            font = new XFont("Consolas", 8, XFontStyle.Regular);
            while (gfx.MeasureString(item.id, font).Width > XUnit.FromCentimeter(Config.Width - 0.2)) {
                font = new XFont("Consolas", font.Size - 0.1, XFontStyle.Regular);
            }
            gfx.DrawString(item.id,
                font,
                XBrushes.Gray,
                new XRect(x, y, XUnit.FromCentimeter(Config.Width), XUnit.FromCentimeter(Config.Height)),
                XStringFormats.BottomCenter);
        }
    }
}
