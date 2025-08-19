using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using PDFFiller.Models;

namespace PDFFiller
{
    class FormFiller
    {
        private FileInfo pdfFile;
        private List<FormField>? formFields;

        public FormFiller(FileInfo pdfFile, string jsonData)
        {
            this.pdfFile = pdfFile;
            this.formFields = JsonConvert.DeserializeObject<List<FormField>>(jsonData);
        }

        public bool FillForm()
        {
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    PdfReader pdfReader = new PdfReader(this.pdfFile.FullName);
                    PdfStamper pdfStamper = new PdfStamper(pdfReader, memoryStream);

                    BaseFont baseFont = BaseFont.CreateFont(
                        BaseFont.TIMES_ROMAN,
                        BaseFont.WINANSI,
                        BaseFont.NOT_EMBEDDED
                    );

                    if (formFields == null)
                    {
                        return false;
                    }

                    foreach (var formField in formFields)
                    {
                        if (formField.Value == null || formField.PageNumber == null)
                        {
                            continue;
                        }

                        pdfStamper.AcroFields.RemoveField(formField.Name);

                        PdfContentByte cb = pdfStamper.GetOverContent(
                            (int)formField.PageNumber.Value
                        );

                        if (IsBase64Image(formField.Value))
                        {
                            AddImageToField(cb, formField);
                        }
                        else if (formField.JsonFieldType == "checkbox")
                        {
                            AddCheckboxToField(cb, formField, baseFont);
                        }
                        else
                        {
                            AddTextToField(cb, formField, baseFont);
                        }
                    }

                    pdfStamper.FormFlattening = true;
                    pdfStamper.Close();

                    byte[] pdfOutput = memoryStream.ToArray();

                    using var stdout = Console.OpenStandardOutput();
                    stdout.Write(pdfOutput);
                    stdout.Flush();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"An error occurred while trying to save the PDF: {ex.Message}"
                );
                Console.Error.Close();

                return false;
            }
        }

        private bool IsBase64Image(string value)
        {
            Regex regex = new Regex(@"^data:image/(?<mediaType>[^;]+);base64,(?<data>.*)");
            return regex.IsMatch(value);
        }

        private void AddImageToField(PdfContentByte cb, FormField formField)
        {
            try
            {
                Regex regex = new Regex(@"^data:image/(?<mediaType>[^;]+);base64,(?<data>.*)");
                Match match = regex.Match(formField.Value);

                if (match.Success)
                {
                    byte[] imageData = Convert.FromBase64String(match.Groups["data"].Value);
                    Image image = Image.GetInstance(imageData);

                    // Scale image to fit within the specified dimensions
                    if (formField.JsonWidth.HasValue && formField.JsonHeight.HasValue)
                    {
                        image.ScaleToFit(formField.JsonWidth.Value, formField.JsonHeight.Value);
                    }

                    float fieldX = formField.Left ?? 0;
                    float fieldY = (formField.Top ?? 0) - formField.Height - image.ScaledHeight;

                    float centeredX = fieldX + (formField.JsonWidth.Value - image.ScaledWidth) / 2;
                    float centeredY =
                        fieldY + (formField.JsonHeight.Value - image.ScaledHeight) / 2;

                    image.SetAbsolutePosition(centeredX, centeredY);
                    cb.AddImage(image);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Error adding image to field {formField.Name}: {ex.Message}"
                );
            }
        }

        private void AddTextToField(PdfContentByte cb, FormField formField, BaseFont baseFont)
        {
            try
            {
                float fontSize = formField.FontSize ?? 10.0f;
                string text = formField.Value ?? "";

                // Handle multi-line text
                if (text.Contains('\n'))
                {
                    AddMultiLineText(cb, formField, baseFont, fontSize, text);
                }
                else
                {
                    AddSingleLineText(cb, formField, baseFont, fontSize, text);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Error adding text to field {formField.Name}: {ex.Message}"
                );
            }
        }

        private void AddSingleLineText(
            PdfContentByte cb,
            FormField formField,
            BaseFont baseFont,
            float fontSize,
            string text
        )
        {
            cb.BeginText();
            cb.SetFontAndSize(baseFont, fontSize);

            (float x, float y) = CalculateTextPosition(formField, text, fontSize, baseFont);

            cb.ShowTextAligned(GetTextAlignment(formField.TextAlign), text, x, y, 0);
            cb.EndText();
        }

        private void AddMultiLineText(
            PdfContentByte cb,
            FormField formField,
            BaseFont baseFont,
            float fontSize,
            string text
        )
        {
            string[] lines = text.Split('\n');
            float lineHeight = fontSize * 1.2f; // Standard line height
            float totalTextHeight = lines.Length * lineHeight;

            float fieldTop = formField.Top ?? 0;
            float fieldBottom = fieldTop - formField.JsonHeight.Value;
            float fieldHeight = formField.JsonHeight.Value;

            cb.BeginText();
            cb.SetFontAndSize(baseFont, fontSize);

            float startY;

            switch (formField.TextAlign)
            {
                case TextAlign.TopLeft:
                case TextAlign.TopCenter:
                case TextAlign.TopRight:
                    startY = fieldTop - fontSize;
                    break;

                case TextAlign.CenterLeft:
                case TextAlign.CenterCenter:
                case TextAlign.CenterRight:
                    startY = fieldBottom + (fieldHeight + totalTextHeight) / 2 - lineHeight;
                    break;

                case TextAlign.BottomLeft:
                case TextAlign.BottomCenter:
                case TextAlign.BottomRight:
                default:
                    startY = fieldBottom + totalTextHeight - lineHeight + fontSize * 0.3f;
                    break;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = startY - (i * lineHeight);
                (float x, float _) = CalculateTextPosition(formField, lines[i], fontSize, baseFont);

                cb.ShowTextAligned(GetTextAlignment(formField.TextAlign), lines[i], x, lineY, 0);
            }
            cb.EndText();
        }

        private (float, float) CalculateTextPosition(
            FormField formField,
            string text,
            float fontSize,
            BaseFont baseFont
        )
        {
            float fieldLeft = formField.Left ?? 0;
            float fieldTop = formField.Top ?? 0;
            float fieldWidth = formField.JsonWidth.Value;
            float fieldHeight = formField.JsonHeight.Value;
            float textWidth = MeasureTextWidth(text, fontSize, baseFont);

            float fieldBottom = fieldTop - fieldHeight;
            float fieldRight = fieldLeft + fieldWidth;

            float finalX = fieldLeft;

            switch (formField.TextAlign)
            {
                case TextAlign.TopCenter:
                case TextAlign.CenterCenter:
                case TextAlign.BottomCenter:
                    finalX = fieldLeft + (fieldWidth / 2); // Center of field for ALIGN_CENTER
                    break;

                case TextAlign.TopRight:
                case TextAlign.CenterRight:
                case TextAlign.BottomRight:
                    finalX = fieldRight; // Right edge for ALIGN_RIGHT
                    break;

                default:
                    finalX = fieldLeft;
                    break;
            }

            float finalY;

            switch (formField.TextAlign)
            {
                case TextAlign.TopLeft:
                case TextAlign.TopCenter:
                case TextAlign.TopRight:
                    // Position near top of field
                    finalY = fieldTop - fontSize;
                    break;

                case TextAlign.CenterLeft:
                case TextAlign.CenterCenter:
                case TextAlign.CenterRight:
                    // Position in vertical center - text baseline at center
                    finalY = fieldBottom + (fieldHeight / 2) - (fontSize * 0.3f);
                    break;

                case TextAlign.BottomLeft:
                case TextAlign.BottomCenter:
                case TextAlign.BottomRight:
                default:
                    // Position near bottom of field
                    finalY = fieldBottom;
                    break;
            }

            return (finalX, finalY);
        }

        private int GetTextAlignment(TextAlign? textAlign)
        {
            return textAlign switch
            {
                TextAlign.TopCenter or TextAlign.CenterCenter or TextAlign.BottomCenter =>
                    PdfContentByte.ALIGN_CENTER,
                TextAlign.TopRight or TextAlign.CenterRight or TextAlign.BottomRight =>
                    PdfContentByte.ALIGN_RIGHT,
                _ => PdfContentByte.ALIGN_LEFT,
            };
        }

        private float MeasureTextWidth(string text, float fontSize, BaseFont baseFont)
        {
            return baseFont.GetWidthPoint(text, fontSize);
        }

        private void AddCheckboxToField(PdfContentByte cb, FormField formField, BaseFont baseFont)
        {
            try
            {
                // Check if the checkbox should be checked
                bool isChecked = IsCheckboxChecked(formField.Value);

                if (!isChecked)
                {
                    return;
                }

                float x = formField.Left ?? 0;
                float y = (formField.Top ?? 0) - formField.JsonHeight.Value;
                float width = formField.JsonWidth.Value;
                float height = formField.JsonHeight.Value;

                DrawCheckmarkSymbol(cb, baseFont, x, y, width, height);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Error adding checkbox to field {formField.Name}: {ex.Message}"
                );
            }
        }

        private bool IsCheckboxChecked(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string lowerValue = value.ToLower().Trim();
            return lowerValue == "true"
                || lowerValue == "yes"
                || lowerValue == "1"
                || lowerValue == "checked"
                || lowerValue == "on";
        }

        private void DrawCheckmarkSymbol(
            PdfContentByte cb,
            BaseFont baseFont,
            float x,
            float y,
            float width,
            float height
        )
        {
            // Draw a white background to cover any existing checkbox
            cb.SetGrayFill(1f); // White fill
            cb.Rectangle(x, y, width, height);
            cb.Fill();

            // Draw border around checkbox
            cb.SetGrayStroke(0f); // Black border
            cb.SetLineWidth(1f);
            cb.Rectangle(x, y, width, height);
            cb.Stroke();

            // Draw checkmark on top
            float fontSize = Math.Min(width, height) * 0.6f; // Scale font to fit the box

            cb.BeginText();
            cb.SetGrayFill(0f);
            cb.SetFontAndSize(baseFont, fontSize);

            // Center the checkmark in the field
            float textX = x + (width / 2);
            float textY = y + (height - fontSize) / 2;

            // Draw a checkmark
            cb.SetGrayStroke(0f);
            cb.SetLineWidth(1f);

            float centerX = x + width / 2;
            float centerY = y + height / 2;
            float size = Math.Min(width, height) * 0.3f;

            cb.MoveTo(centerX - size, centerY);
            cb.LineTo(centerX - size / 3, centerY - size / 2);
            cb.LineTo(centerX + size, centerY + size / 2);
            cb.Stroke();

            cb.EndText();
        }

        private void DrawDebugRectangle(PdfContentByte cb, FormField formField)
        {
            float bottom = formField.Top.Value - formField.JsonHeight.Value;

            cb.SetGrayStroke(0f);
            cb.SetLineWidth(1f);
            cb.Rectangle(
                formField.Left.Value,
                bottom,
                formField.JsonWidth.Value,
                formField.JsonHeight.Value
            );
            cb.Stroke();
        }
    }
}
