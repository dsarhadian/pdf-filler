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
                    AcroFields acroFields = pdfStamper.AcroFields;

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
                        if (formField.Value == null)
                        {
                            continue;
                        }

                        int typeInt = acroFields.GetFieldType(formField.Name);

                        if (
                            (FieldType)typeInt == FieldType.FIELD_TYPE_PUSHBUTTON
                            || (FieldType)typeInt == FieldType.FIELD_TYPE_SIGNATURE
                        )
                        {
                            Regex regex = new Regex(
                                @"^data:image/(?<mediaType>[^;]+);base64,(?<data>.*)"
                            );
                            Match match = regex.Match(formField.Value);

                            if (match.Length == 0)
                            {
                                acroFields.SetField(formField.Name, formField.Value);
                            }
                            else
                            {
                                var fieldPosition = acroFields.GetFieldPositions(formField.Name);
                                Rectangle rect = new Rectangle(
                                    fieldPosition[1],
                                    fieldPosition[2],
                                    fieldPosition[3],
                                    fieldPosition[4]
                                );
                                Image image = Image.GetInstance(
                                    Convert.FromBase64String(match.Groups["data"].Value)
                                );

                                image.ScaleToFit(rect.Width, rect.Height);

                                image.SetAbsolutePosition(rect.Left, rect.Bottom);
                                pdfStamper.GetOverContent((int)fieldPosition[0]).AddImage(image);
                            }
                        }
                        else if (
                            (FieldType)typeInt == FieldType.FIELD_TYPE_TEXT
                            && !formField.Value.Contains('\n')
                        )
                        {
                            var fieldPositions = acroFields.GetFieldPositions(formField.Name);
                            if (fieldPositions != null && fieldPositions.Length == 5)
                            {
                                float fontSize = formField.FontSize ?? 10.0f;

                                int page = (int)fieldPositions[0];
                                PdfContentByte cb = pdfStamper.GetOverContent(page);
                                cb.BeginText();
                                cb.SetFontAndSize(baseFont, fontSize);

                                (float, float) pos = PositionField(formField, fieldPositions);

                                cb.ShowTextAligned(
                                    PdfContentByte.ALIGN_LEFT,
                                    formField.Value,
                                    pos.Item1,
                                    pos.Item2,
                                    0
                                );
                                cb.EndText();
                            }

                            // Remove the actual form field
                            acroFields.RemoveField(formField.Name);
                        }
                        else
                        {
                            acroFields.SetFieldProperty(
                                formField.Name,
                                "textsize",
                                formField.FontSize ?? 10.0f,
                                null
                            );
                            acroFields.SetFieldProperty(formField.Name, "textfont", baseFont, null);
                            acroFields.SetField(formField.Name, formField.Value);
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
                    $"An error ocurred while trying to file the PDF: {ex.Message}"
                );
                Console.Error.Close();

                return false;
            }
        }

        // private (float, float) PositionField(FormField field, float[] positions)
        // {
        //     int page = (int)positions[0];
        //     float left = positions[1];
        //     float bottom = positions[2];
        //     float right = positions[3];
        //     float top = positions[4];
        //     float fontSize = field.FontSize ?? 10.0f;
        //     float fieldHeight = top - bottom;
        //     float fieldWidth = right - left;
        //
        //     float x = 0;
        //     float y = 0;
        //
        //     switch (field.TextAlign)
        //     {
        //         case TextAlign.TopLeft:
        //             x = left;
        //             y = top - fontSize;
        //             break;
        //
        //         case TextAlign.TopCenter:
        //             x = left + (fieldWidth / 2);
        //             y = top - fontSize;
        //             break;
        //
        //         case TextAlign.TopRight:
        //             x = right;
        //             y = top - fontSize;
        //             break;
        //
        //         case TextAlign.CenterLeft:
        //             x = left;
        //             y = bottom + (fieldHeight - fontSize) / 2;
        //             break;
        //
        //         case TextAlign.CenterCenter:
        //             x = left + (fieldWidth / 2);
        //             y = bottom + (fieldHeight - fontSize) / 2;
        //             break;
        //
        //         case TextAlign.CenterRight:
        //             x = right;
        //             y = bottom + (fieldHeight - fontSize) / 2;
        //             break;
        //
        //         case TextAlign.BottomLeft:
        //             x = left;
        //             y = bottom;
        //             break;
        //
        //         case TextAlign.BottomCenter:
        //             x = left + (fieldWidth / 2);
        //             y = bottom;
        //             break;
        //
        //         case TextAlign.BottomRight:
        //             x = right;
        //             y = bottom;
        //             break;
        //     }
        //
        //     return (x, y);
        // }

        private float MeasureTextWidth(string text, float fontSize)
        {
            BaseFont baseFont = BaseFont.CreateFont(
                BaseFont.TIMES_ROMAN,
                BaseFont.WINANSI,
                BaseFont.NOT_EMBEDDED
            );

            return baseFont.GetWidthPoint(text, fontSize);
        }

        private (float, float) PositionField(FormField field, float[] positions)
        {
            int page = (int)positions[0];
            float left = positions[1];
            float bottom = positions[2];
            float right = positions[3];
            float top = positions[4];
            float fontSize = field.FontSize ?? 10.0f;

            float fieldHeight = top - bottom;
            float fieldWidth = right - left;

            string text = field.Value ?? "";
            float textWidth = MeasureTextWidth(text, fontSize);

            float x = 0;
            float y = 0;

            switch (field.TextAlign)
            {
                case TextAlign.TopLeft:
                    x = left;
                    y = top - fontSize;
                    break;

                case TextAlign.TopCenter:
                    x = left + (fieldWidth - textWidth) / 2;
                    y = top - fontSize;
                    break;

                case TextAlign.TopRight:
                    x = right - textWidth;
                    y = top - fontSize;
                    break;

                case TextAlign.CenterLeft:
                    x = left;
                    y = bottom + (fieldHeight - fontSize) / 2;
                    break;

                case TextAlign.CenterCenter:
                    x = left + (fieldWidth - textWidth) / 2;
                    y = bottom + (fieldHeight - fontSize) / 2;
                    break;

                case TextAlign.CenterRight:
                    x = right - textWidth;
                    y = bottom + (fieldHeight - fontSize) / 2;
                    break;

                case TextAlign.BottomLeft:
                    x = left;
                    y = bottom;
                    break;

                case TextAlign.BottomCenter:
                    x = left + (fieldWidth - textWidth) / 2;
                    y = bottom;
                    break;

                case TextAlign.BottomRight:
                    x = right - textWidth;
                    y = bottom;
                    break;
            }

            return (x, y);
        }
    }
}
