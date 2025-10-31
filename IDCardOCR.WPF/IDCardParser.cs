using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Diagnostics; // For Debug.WriteLine
using System.Collections.Generic;

namespace IDCardOCR.WPF
{
    public static class IDCardParser
    {
        // Pre-compiled Regex patterns for performance
        private static readonly Regex RegexNormalizeTeaFa = new Regex("茶发机关", RegexOptions.Compiled);
        private static readonly Regex RegexNormalizeValidPeriod = new Regex("有效期解", RegexOptions.Compiled);
        private static readonly Regex RegexNormalizeCitizenId = new Regex("公民9编号编", RegexOptions.Compiled);

        private static readonly Regex RegexRemoveSpaces = new Regex("\\s", RegexOptions.Compiled);
        private static readonly Regex RegexIdNumberPattern = new Regex("\\d{17}[0-9Xx]", RegexOptions.Compiled);
        private static readonly Regex RegexIdNumberL = new Regex("l", RegexOptions.Compiled);
        private static readonly Regex RegexIdNumberO = new Regex("o", RegexOptions.Compiled);
        private static readonly Regex RegexIdNumberS = new Regex("s", RegexOptions.Compiled);

        private static readonly Regex RegexNoiseHeuristic = new Regex("^\\s*[\\d\\s]+$|^[A-Za-z\\s]+$", RegexOptions.Compiled);
        private static readonly Regex RegexNonChineseNonAlphanumeric = new Regex("[^\\u4E00-\\u9FFF A-Za-z0-9 ]", RegexOptions.Compiled);

        private static readonly Regex RegexFormatDateWhitespace = new Regex("\\s+", RegexOptions.Compiled);
        private static readonly Regex RegexFormatDateSeparator = new Regex("[.,_\\-]+\\d", RegexOptions.Compiled);

        private static readonly Regex RegexCorrectIdNumberYear = new Regex("\\d{4}", RegexOptions.Compiled);

        private static readonly string[] ALL_KEYWORDS = { "姓名", "民族", "住址", "公民身份号码", "签发机关", "有效期限", "性别", "出生" };

        public static IDCardInfo Parse(string ocrText)
        {
            IDCardInfo info = new IDCardInfo { RawOcrText = ocrText };

            // 0. Preprocess OCR text to remove noise
            ocrText = PreprocessOcrText(ocrText);

            // 1. Normalize keywords and common OCR errors
            string textToParse = NormalizeKeywords(ocrText);

            // 2. Prioritize extracting ID Number
            info.身份证号码 = ExtractIdNumber(textToParse);
            
            bool isIdNumberValid = ValidateIdNumber(info.身份证号码 ?? string.Empty);
            if (isIdNumberValid)
            {
                            info.身份证号码Status = FieldStatus.Success;
                            // 3. Calculate derived fields from ID number
                            info.性别 = GetGenderFromID(info.身份证号码 ?? string.Empty);
                            info.性别Status = string.IsNullOrEmpty(info.性别) ? FieldStatus.NotFound : FieldStatus.Success;
                            info.年龄 = GetAgeFromID(info.身份证号码 ?? string.Empty);
                            info.年龄Status = string.IsNullOrEmpty(info.年龄) ? FieldStatus.NotFound : FieldStatus.Success;
                            info.出生日期 = GetBirthDateFromID(info.身份证号码 ?? string.Empty);
                            info.出生日期Status = string.IsNullOrEmpty(info.出生日期) ? FieldStatus.NotFound : FieldStatus.Success;
                        }
                        else
                        {
                            info.身份证号码Status = FieldStatus.LowQuality;
                            // If ID number is invalid or not found, try to extract from OCR
                            // Use more robust patterns for direct OCR extraction of Sex and BirthDate
                            info.性别 = ExtractField(textToParse, @"(?:性别|性別)\s*(男|女)");
                            info.性别Status = string.IsNullOrEmpty(info.性别) ? FieldStatus.NotFound : FieldStatus.Success;
                            info.出生日期 = ExtractField(textToParse, @"(?:出生|出生日期)\s*(\d{4}年\d{1,2}月\d{1,2}日)");
                            info.出生日期Status = string.IsNullOrEmpty(info.出生日期) ? FieldStatus.NotFound : FieldStatus.Success;
                            // Age cannot be reliably extracted without a valid ID number or birth date, so mark as NotFound
                            info.年龄 = "";
                            info.年龄Status = FieldStatus.NotFound;
                        }            
            // 4. Extract other fields using the new robust extraction method
            info.姓名 = ExtractField(textToParse, new[] { "姓名", "姓名:" }, FieldType.Name);
            info.姓名Status = ValidateField(info.姓名, FieldType.Name);
            info.民族 = ExtractField(textToParse, new[] { "民族", "民族:" }, FieldType.Ethnicity);
            info.民族Status = ValidateField(info.民族, FieldType.Ethnicity);
            info.住址 = ExtractField(textToParse, new[] { "住址", "住址:" }, FieldType.Address);
            info.住址Status = ValidateField(info.住址, FieldType.Address);
            info.发行机关 = ExtractField(textToParse, new[] { "签发机关", "发证机关", "茶发机关" }, FieldType.IssuingAuthority);
            info.发行机关Status = ValidateField(info.发行机关, FieldType.IssuingAuthority);
            info.有效期 = ExtractField(textToParse, new[] { "有效期限", "有效期解" }, FieldType.ExpirationDate);
            info.有效期Status = ValidateField(info.有效期, FieldType.ExpirationDate);

            // Final specific cleanup for fields
            info.姓名 = PostProcessField(CleanFieldSpecificNoise(info.姓名, FieldType.Other), FieldType.Name);
            info.民族 = PostProcessField(CleanFieldSpecificNoise(info.民族, FieldType.Other), FieldType.Ethnicity);
            info.住址 = PostProcessField(CleanFieldSpecificNoise(info.住址, FieldType.Address), FieldType.Address);
            info.发行机关 = PostProcessField(CleanFieldSpecificNoise(info.发行机关, FieldType.Other), FieldType.IssuingAuthority);
            info.有效期 = PostProcessField(CleanFieldSpecificNoise(info.有效期, FieldType.ExpirationDate), FieldType.ExpirationDate);

            // Format Expiration Date
            // info.有效期 = FormatExpirationDate(info.有效期);

            return info;
        }

        private enum FieldType { IDNumber, Address, Other, Name, Ethnicity, IssuingAuthority, ExpirationDate, BirthDate }

        private static string NormalizeKeywords(string ocrText)
        {
            // Normalize common OCR errors in keywords
            ocrText = RegexNormalizeTeaFa.Replace(ocrText, "签发机关");
            ocrText = RegexNormalizeValidPeriod.Replace(ocrText, "有效期限");
            ocrText = RegexNormalizeCitizenId.Replace(ocrText, "公民身份号码");
            return ocrText;
        }

        private static string? ExtractIdNumber(string text)
        {
            // Python's approach: remove all spaces from the corpus, then search for the ID pattern.
            string noSpaceText = RegexRemoveSpaces.Replace(text, "");
            Match match = RegexIdNumberPattern.Match(noSpaceText); 
            if (match.Success)
            {
                string? id = match.Groups[0].Value.Trim(); // Group 0 is the entire match
                // Post-process: some common OCR errors for ID numbers
                id = RegexIdNumberL.Replace(id, "1");
                id = RegexIdNumberO.Replace(id, "0");
                id = RegexIdNumberS.Replace(id, "5");
                // Add more common ID number OCR error corrections if needed
                return id;
            }
            return null;
        }

        private static string? CleanFieldSpecificNoise(string? fieldText, FieldType type)
        {
            if (string.IsNullOrEmpty(fieldText)) return fieldText;

            if (type == FieldType.IDNumber)
            {
                // Already handled in ExtractIdNumber, but good to have a placeholder
                return fieldText;
            }
            else if (type == FieldType.Address)
            {
                // For Address, retain almost all characters, only simple cleanup
                return fieldText.Trim();
            }
            else if (type == FieldType.ExpirationDate)
            {
                // For ExpirationDate, allow . and - characters
                return Regex.Replace(fieldText, "[^\\u4E00-\\u9FFF A-Za-z0-9.\\- ]", "").Trim();
            }
            else // FieldType.Other
            {
                // Remove non-Chinese, non-alphanumeric, non-space characters excluding X for other fields
                return RegexNonChineseNonAlphanumeric.Replace(fieldText, "").Trim();
            }
        }

        private static string PreprocessOcrText(string ocrText)
        {
            // Replace multiple spaces with a single space
            string cleanedText = RegexFormatDateWhitespace.Replace(ocrText, " ");

            return cleanedText.Trim();
        }

        private static string? PostProcessField(string? fieldText, FieldType type)
        {
            if (string.IsNullOrEmpty(fieldText)) return fieldText;

            switch (type)
            {
                case FieldType.Name:
                    // Name should primarily be Chinese characters
                    return Regex.Replace(fieldText, "[^\\u4E00-\\u9FFF]", "").Trim();
                case FieldType.Ethnicity:
                    // Ethnicity should primarily be Chinese characters
                    return Regex.Replace(fieldText, "[^\\u4E00-\\u9FFF]", "").Trim();
                case FieldType.Address:
                    // Address can be complex, remove all spaces
                    return fieldText.Replace(" ", "").Trim();
                case FieldType.IssuingAuthority:
                    // Issuing authority should primarily be Chinese characters
                    return Regex.Replace(fieldText, "[^\\u4E00-\\u9FFF]", "").Trim();
                case FieldType.ExpirationDate:
                    // Expiration date will be handled by FormatExpirationDate, but some initial cleaning
                    return fieldText.Replace(" ", "").Trim();
                case FieldType.IDNumber:
                    // ID number already heavily processed
                    return fieldText.Trim();
                default:
                    return fieldText.Trim();
            }
        }

        private static string GetBirthDateFromID(string idNumber)
        {
            if (idNumber.Length >= 14) // Ensure enough characters for date
            {
                string year = idNumber.Substring(6, 4);
                string month = idNumber.Substring(10, 2);
                string day = idNumber.Substring(12, 2);
                // Basic validation for month and day
                if (int.TryParse(month, out int m) && m >= 1 && m <= 12 &&
                    int.TryParse(day, out int d) && d >= 1 && d <= 31)
                {
                    return $"{year}年{month}月{day}日";
                }
            }
            return "";
        }

        private static string GetGenderFromID(string idNumber)
        {
            if (idNumber.Length >= 17) // Ensure enough characters for gender
            {
                if (int.TryParse(idNumber.Substring(16, 1), out int genderCode))
                {
                    return (genderCode % 2 == 0) ? "女" : "男";
                }
            }
            return "";
        }

        private static string GetAgeFromID(string idNumber)
        {
            if (idNumber.Length >= 14)
            {
                string yearStr = idNumber.Substring(6, 4);
                string monthStr = idNumber.Substring(10, 2);
                string dayStr = idNumber.Substring(12, 2);

                if (int.TryParse(yearStr, out int year) &&
                    int.TryParse(monthStr, out int month) &&
                    int.TryParse(dayStr, out int day))
                {
                    DateTime birthDate = new DateTime(year, month, day);
                    DateTime today = DateTime.Today;
                    int age = today.Year - birthDate.Year;
                    if (birthDate.Date > today.AddYears(-age))
                    {
                        age--;
                    }
                    return age.ToString();
                }
            }
            return "";
        }


        private static string? ExtractField(string textToParse, string pattern)
        {
            Match match = Regex.Match(textToParse, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
            return null;
        }

        private static string? ExtractField(string textToParse, string[] keywords, FieldType type)
        {
            foreach (string keyword in keywords)
            {
                // Escape special regex characters in the keyword
                string escapedKeyword = Regex.Escape(keyword);
                // Pattern to find the keyword and capture the text following it
                // Using a non-greedy match for the value and considering line breaks
                string pattern;
                string keywordsPattern = string.Join("|", ALL_KEYWORDS.Select(Regex.Escape));

                if (type == FieldType.ExpirationDate)
                {
                    // Specific pattern for expiration date: YYYY.MM.DD-YYYY.MM.DD or YYYY.MM.DD-长期
                    pattern = $@"{escapedKeyword}\s*(?<value>\d{{4}}\.\d{{2}}\.\d{{2}}-(?:\d{{4}}\.\d{{2}}\.\d{{2}}|长期))";
                }
                else if (type == FieldType.Address)
                {
                    // For address, capture multiple lines until the next keyword or end of text
                    // This pattern allows for newlines within the address but stops at the next keyword
                    pattern = $@"{escapedKeyword}\s*(?<value>(?:(?!{keywordsPattern}).)*)";
                }
                else
                {
                    // For other fields, capture text until the next keyword or end of text
                    pattern = $@"{escapedKeyword}\s*(?<value>.*?)(?=\n?{keywordsPattern}|$)";
                }

                Match match = Regex.Match(textToParse, pattern, RegexOptions.Singleline);
                if (match.Success)
                {
                    string value = match.Groups["value"].Value.Trim();
                    // Remove any trailing keywords that might have been captured due to greedy matching or OCR errors
                    foreach (string otherKeyword in ALL_KEYWORDS)
                    {
                        if (value.EndsWith(otherKeyword))
                        {
                            value = value.Substring(0, value.Length - otherKeyword.Length).Trim();
                        }
                    }
                    return value;
                }
            }
            return null;
        }

        private static bool ValidateIdNumber(string idNumber)
        {
            if (string.IsNullOrEmpty(idNumber) || idNumber.Length != 18)
            {
                return false;
            }
            // Basic pattern check: 17 digits + 1 digit/X/x
            return Regex.IsMatch(idNumber, "^\\d{17}[0-9Xx]$");
        }

        private static FieldStatus ValidateField(string? fieldValue, FieldType type)
        {
            if (string.IsNullOrEmpty(fieldValue)) 
            {
                return FieldStatus.NotFound;
            }

            switch (type)
            {
                case FieldType.Name:
                    // Name should primarily be Chinese characters and not too short
                    if (fieldValue.Length < 2 || Regex.IsMatch(fieldValue, "[^\\u4E00-\\u9FFF]")) 
                    {
                        return FieldStatus.LowQuality;
                    }
                    break;
                case FieldType.Ethnicity:
                    // Ethnicity should primarily be Chinese characters
                    if (Regex.IsMatch(fieldValue, "[^\\u4E00-\\u9FFF]")) 
                    {
                        return FieldStatus.LowQuality;
                    }
                    break;
                case FieldType.Address:
                    // Address should contain some Chinese characters
                    if (!Regex.IsMatch(fieldValue, "[\\u4E00-\\u9FFF]")) 
                    {
                        return FieldStatus.LowQuality;
                    }
                    break;
                case FieldType.IssuingAuthority:
                    // Issuing authority should primarily be Chinese characters
                    if (Regex.IsMatch(fieldValue, "[^\\u4E00-\\u9FFF]")) 
                    {
                        return FieldStatus.LowQuality;
                    }
                    break;
                case FieldType.ExpirationDate:
                    // Should match YYYY.MM.DD-YYYY.MM.DD or YYYY.MM.DD-长期
                    if (!Regex.IsMatch(fieldValue, "^\\d{4}\\.\\d{2}\\.\\d{2}-(?:\\d{4}\\.\\d{2}\\.\\d{2}|长期)$")) 
                    {
                        return FieldStatus.LowQuality;
                    }
                    break;
                case FieldType.BirthDate:
                    // Should match YYYY年MM月DD日
                    if (!Regex.IsMatch(fieldValue, "^\\d{4}年\\d{1,2}月\\d{1,2}日$")) 
                    {
                        return FieldStatus.LowQuality;
                    }
                    break;
            }
            return FieldStatus.Success;
        }
    }
}