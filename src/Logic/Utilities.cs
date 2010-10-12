﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

namespace Nikse.SubtitleEdit.Logic
{
    public static class Utilities
    {
        public static VideoInfo GetVideoInfo(string fileName, EventHandler event1)
        {
            VideoInfo info = TryReadVideoInfoViaAviHeader(fileName);
            if (info.Success)
                return info;

            info = TryReadVideoInfoViaMatroskaHeader(fileName);
            if (info.Success)
                return info;

            info = TryReadVideoInfoViaDirectShow(fileName);
            if (info.Success)
                return info;

            if (IsWmpAvailable)
                return TryReadVideoInfoViaMediaPlayer(fileName, event1);

            return new VideoInfo { VideoCodec = "Unknown" };
        }

        private static VideoInfo TryReadVideoInfoViaDirectShow(string fileName)
        {
            return QuartsPlayer.GetVideoInfo(fileName);
        }

        private static VideoInfo TryReadVideoInfoViaMatroskaHeader(string fileName)
        {
            var info = new VideoInfo { Success = false };

            try
            {
                bool hasConstantFrameRate = false;
                bool success = false;
                double frameRate = 0;
                int width = 0;
                int height = 0;
                double milliseconds = 0;
                string videoCodec = string.Empty;

                var matroskaParser = new Matroska();
                matroskaParser.GetMatroskaInfo(fileName, ref success, ref hasConstantFrameRate, ref frameRate, ref width, ref height, ref milliseconds, ref videoCodec);
                if (success)
                {
                    info.Width = width;
                    info.Height = height;
                    info.FramesPerSecond = frameRate;
                    info.Success = true;
                    info.TotalMilliseconds = milliseconds;
                    info.TotalSeconds = milliseconds / 1000.0;
                    info.TotalFrames = info.TotalSeconds * frameRate;
                    info.VideoCodec = videoCodec;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
            return info;
        }

        private static VideoInfo TryReadVideoInfoViaMediaPlayer(string fileName, EventHandler doEvents)
        {
            var info = new VideoInfo { Success = false };

            try
            {
                var player = new WMPLib.WindowsMediaPlayer { URL = fileName };

                player.controls.play();

                int i = 0;
                while (i < 100 && player.openState != WMPLib.WMPOpenState.wmposMediaOpen)
                {
                    i++;
                    System.Threading.Thread.Sleep(100);
                    if (doEvents != null)
                        doEvents.Invoke(null, null);
                }

                info.TotalSeconds = player.currentMedia.duration;
                info.TotalMilliseconds = player.currentMedia.duration * 1000;

                info.Width = player.currentMedia.imageSourceWidth;
                info.Height = player.currentMedia.imageSourceHeight;
                info.FramesPerSecond = player.network.encodedFrameRate;

                player.controls.stop();
                player.close();
                info.Success = true;
            }
            catch
            {
            }
            return info;
        }

        private static VideoInfo TryReadVideoInfoViaAviHeader(string fileName)
        {
            var info = new VideoInfo { Success = false };

            try
            {
                var rp = new RiffParser();
                var dh = new RiffDecodeHeader(rp);
                rp.OpenFile(fileName);
                info.FileType = RiffParser.FromFourCC(rp.FileType);
                if (RiffParser.ckidAVI == rp.FileType)
                {
                    dh.ProcessMainAVI();
                    info.Width = dh.Width;
                    info.Height = dh.Height;
                    info.FramesPerSecond = dh.FrameRate;
                    info.TotalFrames = dh.TotalFrames;
                    info.TotalMilliseconds = dh.TotalMilliseconds;
                    info.VideoCodec = dh.VideoHandler;
                    info.Success = true;
                }
            }
            catch
            {
            }
            return info;
        }

        public static IEnumerable<string> GetMovieFileExtensions()
        {
            return new List<string> { ".avi", ".mkv", ".wmv", ".mpg", ".mpeg", ".divx", ".mp4", ".asf", ".flv" };
        }

        public static string GetVideoFileFilter()
        {
            var sb = new StringBuilder();
            sb.Append(Configuration.Settings.Language.General.VideoFiles + "|");
            int i = 0;
            foreach (string extension in GetMovieFileExtensions())
            {
                if (i > 0)
                    sb.Append(";");
                sb.Append("*" + extension);
                i++;
            }
            sb.Append("|" + Configuration.Settings.Language.General.AllFiles + "|*.*");
            return sb.ToString();
        }

        public static bool IsInteger(string s)
        {
            int i;
            if (int.TryParse(s, out i))
                return true;
            return false;
        }

        internal static SubtitleFormat GetSubtitleFormatByFriendlyName(string friendlyName)
        {
            foreach (SubtitleFormat format in SubtitleFormat.AllSubtitleFormats)
            {
                if (format.FriendlyName == friendlyName)
                    return format;
            }
            return null;
        }

        public static string FormatBytesToDisplayFileSize(long fileSize)
        {
            {
                if (fileSize <= 1024)
                    return string.Format("{0} bytes", fileSize);
                if (fileSize <= 1024 * 1024)
                    return string.Format("{0} kb", fileSize / 1024);
                if (fileSize <= 1024 * 1024 * 1024)
                    return string.Format("{0:0.0} mb", (float)fileSize / (1024 * 1024));
                return string.Format("{0:0.0} gb", (float)fileSize / (1024 * 1024 * 1024));
            }
        }

        public static int ShowSubtitle(List<Paragraph> paragraphs, Label labelSubtitle, VideoPlayer mediaPlayer)
        {
            int index = 0;
            if (mediaPlayer != null)
            {
                double positionInMilliseconds = mediaPlayer.CurrentPosition * 1000.0;
                string text = string.Empty;
                foreach (Paragraph p in paragraphs)
                {
                    if (p.StartTime.TotalMilliseconds <= positionInMilliseconds &&
                        p.EndTime.TotalMilliseconds > positionInMilliseconds)
                    {
                        text = p.Text.Replace("|", Environment.NewLine);
                        break;
                    }
                    index++;
                }
                if (index == paragraphs.Count)
                    index = -1;

                if (labelSubtitle.Text != text)
                {
                    labelSubtitle.Text = text;
                    return index;
                }
            }
            return -1;
        }

        public static string ReadTextFileViaUrlAndProxyIfAvailable(string url)
        {
            var wc = new WebClient {Proxy = GetProxy()};
            var ms = new MemoryStream(wc.DownloadData(url));
            var reader = new StreamReader(ms);
            return reader.ReadToEnd().Trim();
        }

        public static WebProxy GetProxy()
        {
            if (!string.IsNullOrEmpty(Configuration.Settings.Proxy.ProxyAddress))
            {
                var proxy = new WebProxy(Configuration.Settings.Proxy.ProxyAddress);

                if (!string.IsNullOrEmpty(Configuration.Settings.Proxy.UserName))
                {
                    if (string.IsNullOrEmpty(Configuration.Settings.Proxy.Domain))
                        proxy.Credentials = new NetworkCredential(Configuration.Settings.Proxy.UserName, Configuration.Settings.Proxy.DecodePassword());
                    else
                        proxy.Credentials = new NetworkCredential(Configuration.Settings.Proxy.UserName, Configuration.Settings.Proxy.DecodePassword(), Configuration.Settings.Proxy.Domain);
                }
                else
                    proxy.UseDefaultCredentials = true;
                
                return proxy;
            }
            return null;
        }

        private static bool IsPartOfNumber(string s, int position)
        { 
            if (",.".Contains(s[position].ToString()))
            {
                if (position > 0 && position < s.Length-1)
                {
                    return "1234567890".Contains(s[position-1].ToString()) && "1234567890".Contains(s[position+1].ToString());
                }
            }
            return false;
        }

        public static string AutoBreakLine(string text)
        {
            return AutoBreakLine(text, 5);
        }

        public static string AutoBreakLine(string text, int mininumLength)
        {
            if (text.Length < 5)
                return text;

            string temp = RemoveHtmlTags(text);
            temp = temp.TrimEnd("!?.:;".ToCharArray());
            if (text.Length < 40 && !temp.Contains(".") && !temp.Contains("!") && !temp.Contains("?"))
            {
                text = text.Replace(Environment.NewLine, " ");
                text = text.Replace("  ", " ");
                text = text.Replace("  ", " ");
                return text;
            }

            string s = text.Replace(Environment.NewLine, " ");
            s = s.Replace("  ", " ");
            s = s.Replace("  ", " ");
            int splitPos = -1;
            int mid = s.Length / 2;

            if (splitPos == -1)
            {
                for (int j = 0; j < 15; j++)
                {
                    if (mid + j + 1 < s.Length && mid + j > 0)
                    {
                        if (".!?".Contains(s[mid + j].ToString()) && !IsPartOfNumber(s, mid + j))
                        {
                            splitPos = mid + j + 1;
                            if (".!?0123456789".Contains(s[splitPos].ToString()))
                            { // do not break double/tripple end lines like "!!!" or "..."
                                splitPos++;
                                if (".!?0123456789".Contains(s[mid + j + 1].ToString()))
                                    splitPos++;
                            }
                            break;
                        }
                        if (".!?".Contains(s[mid - j].ToString()) && !IsPartOfNumber(s, mid - j))
                        {
                            splitPos = mid - j;
                            splitPos++;
                            break;
                        }
                    }
                }
            }

            if (splitPos == -1)
            {
                for (int j = 0; j < 25; j++)
                {
                    if (mid + j + 1 < s.Length && mid + j > 0)
                    {
                        if (".!?, ".Contains(s[mid + j].ToString()) && !IsPartOfNumber(s, mid + j))
                        {
                            splitPos = mid + j;
                            if (" .!?".Contains(s[mid + j + 1].ToString()))
                            {
                                splitPos++;
                                if (" .!?".Contains(s[mid + j + 2].ToString()))
                                    splitPos++;
                            }
                            break;
                        }
                        if (".!?, ".Contains(s[mid - j].ToString()) && !IsPartOfNumber(s, mid - j))
                        {
                            splitPos = mid - j;
                            if (".!?".Contains(s[mid - (j + 1)].ToString()))
                            {
                                splitPos--;
                                if (".!?".Contains(s[mid + (j + 2)].ToString()))
                                    splitPos--;
                            }
                            break;
                        }
                    }
                }
            }

            if (splitPos == -1)
            {
                splitPos = mid;
                s = s.Insert(mid - 1, "-");
            }
            if (splitPos < s.Length-2)
                s = s.Substring(0, splitPos).TrimEnd() + Environment.NewLine + s.Substring(splitPos).Trim();
            return s.TrimEnd();
        }

        public static string UnbreakLine(string text)
        {
            if (text.Contains(Environment.NewLine))
            {
                string newText = text.Replace(Environment.NewLine, " ");
                while (newText.Contains("  "))
                    newText = newText.Replace("  ", " ");
                return newText;
            }
            return text;
        }

        public static void InitializeSubtitleFont(Control control)
        {
            var gs = Configuration.Settings.General;

            if (string.IsNullOrEmpty(gs.SubtitleFontName))
                gs.SubtitleFontName = "Microsoft Sans Serif";

            if (gs.SubtitleFontBold)
                control.Font = new System.Drawing.Font(gs.SubtitleFontName, gs.SubtitleFontSize, System.Drawing.FontStyle.Bold);
            else
                control.Font = new System.Drawing.Font(gs.SubtitleFontName, gs.SubtitleFontSize);
        }

        public static string RemoveHtmlTags(string s)
        {
            s = s.Replace("<i>", string.Empty);
            s = s.Replace("</i>", string.Empty);
            s = s.Replace("<b>", string.Empty);
            s = s.Replace("</b>", string.Empty);
            s = s.Replace("<u>", string.Empty);
            s = s.Replace("</u>", string.Empty);
            s = s.Replace("<I>", string.Empty);
            s = s.Replace("</I>", string.Empty);
            s = s.Replace("<B>", string.Empty);
            s = s.Replace("</B>", string.Empty);
            s = s.Replace("<U>", string.Empty);
            s = s.Replace("</U>", string.Empty);
            return RemoveHtmlFontTag(s);
        }

        internal static string RemoveHtmlFontTag(string s)
        {
            s = s.Replace("</font>", string.Empty);
            s = s.Replace("</FONT>", string.Empty);
            s = s.Replace("</Font>", string.Empty);
            s = s.Replace("<font>", string.Empty);
            s = s.Replace("<FONT>", string.Empty);
            s = s.Replace("<Font>", string.Empty);
            while (s.ToLower().Contains("<font"))
            {
                int startIndex = s.ToLower().IndexOf("<font");
                int endIndex = Math.Max(s.IndexOf(">"), startIndex + 4); 
                s = s.Remove(startIndex, (endIndex - startIndex) + 1);
            }
            return s;
        }

        public static Encoding GetEncodingFromFile(string fileName)
        {
            Encoding encoding =  Encoding.Default;
            try
            {
                var file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

                var bom = new byte[12]; // Get the byte-order mark, if there is one
                file.Position = 0;
                file.Read(bom, 0, 12);
                if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                    encoding =  Encoding.UTF8;
                else if (bom[0] == 0xff && bom[1] == 0xfe)
                    encoding =  Encoding.Unicode;
                else if (bom[0] == 0xfe && bom[1] == 0xff) // utf-16 and ucs-2
                    encoding =  Encoding.BigEndianUnicode;
                else if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) // ucs-4
                    encoding =  Encoding.UTF32;
                else if (encoding == Encoding.Default && file.Length > 12)
                {
                    int length = (int)file.Length;
                    if (length > 100000)
                        length = 100000;

                    file.Position = 0;
                    var buffer = new byte[length];
                    file.Read(buffer, 0, length);

                    if (IsUtf8(buffer))
                    {
                        encoding = Encoding.UTF8;
                    }
                    else if (Configuration.Settings.General.AutoGuessAnsiEncoding)
                    {
                        encoding = DetectAnsiEncoding(buffer);

                        Encoding greekEncoding = Encoding.GetEncoding(1253); // Greek
                        if (GetCount(greekEncoding.GetString(buffer), "μου", "είναι", "Είναι", "αυτό", "Τόμπυ", "καλά") > 5)
                            return greekEncoding;

                        Encoding russianEncoding = Encoding.GetEncoding(1251); // Russian
                        if (GetCount(russianEncoding.GetString(buffer), "что", "быть", "весь", "этот", "один", "такой") > 5)
                            return russianEncoding;
                        russianEncoding = Encoding.GetEncoding(28595); // Russian
                        if (GetCount(russianEncoding.GetString(buffer), "что", "быть", "весь", "этот", "один", "такой") > 5)
                            return russianEncoding;

                        Encoding arabicEncoding = Encoding.GetEncoding(28596); // Arabic
                        Encoding hewbrewEncoding = Encoding.GetEncoding(28598); // Hebrew
                        if (GetCount(arabicEncoding.GetString(buffer), "من", "هل", "لا", "فى", "لقد", "ما") > 5)
                        {
                            if (GetCount(hewbrewEncoding.GetString(buffer), "אולי", "אולי", "אולי", "אולי", "טוב", "טוב") > 10)
                                return hewbrewEncoding;
                            else
                                return arabicEncoding;
                        }
                        else if (GetCount(hewbrewEncoding.GetString(buffer), "אתה", "אולי", "הוא", "בסדר", "יודע", "טוב") > 5)
                            return hewbrewEncoding;

                    }
                }
                file.Close();
                file.Dispose();
            }
            catch
            {

            }
            return encoding;
        }

        /// <summary>
        /// Will try to determine if buffer is utf-8 encoded or not.
        /// If any non-utf8 sequences are found then false is returned, if no utf8 multibytes sequences are found then false is returned.       
        /// </summary>
        private static bool IsUtf8(byte[] buffer)
        {
            int utf8Count = 0;
            int i = 0;
            while (i < buffer.Length - 3)
            {
                byte b = buffer[i];
                if (b > 127)
                {
                    if (b >= 194 && b <=223 && buffer[i+1] >= 128 &&  buffer[i+1] <= 191)
                    { // 2-byte sequence
                        utf8Count++;
                        i++;
                    }
                    else if (b >= 224 && b <= 239 && buffer[i + 1] >= 128 && buffer[i + 1] <= 191 &&
                                                     buffer[i + 2] >= 128 && buffer[i + 2] <= 191)
                    { // 3-byte sequence
                        utf8Count++;
                        i += 2;
                    }
                    else if (b >= 240 && b <= 244 && buffer[i + 1] >= 128 && buffer[i + 1] <= 191 &&
                                                     buffer[i + 2] >= 128 && buffer[i + 2] <= 191 &&
                                                     buffer[i + 3] >= 128 && buffer[i + 3] <= 191)
                    { // 4-byte sequence
                        utf8Count++;
                        i += 3;
                    }
                    else
                    {
                        return false;
                    }
                }
                i++;
            }
            if (utf8Count == 0)
                return false; // not utf-8            

            return true;
        }

        public static Encoding DetectAnsiEncoding(byte[] buffer)
        {
            if (IsRunningOnMono())
                return Encoding.Default;

            try
            {
                Encoding encoding = DetectEncoding.EncodingTools.DetectInputCodepage(buffer);

                Encoding greekEncoding = Encoding.GetEncoding(1253); // Greek
                if (GetCount(greekEncoding.GetString(buffer), "μου", "είναι", "Είναι", "αυτό", "Τόμπυ", "καλά") > 5)
                    return greekEncoding;

                Encoding russianEncoding = Encoding.GetEncoding(1251); // Russian
                if (GetCount(russianEncoding.GetString(buffer), "что", "быть", "весь", "этот", "один", "такой") > 5)
                    return russianEncoding;
                russianEncoding = Encoding.GetEncoding(28595); // Russian
                if (GetCount(russianEncoding.GetString(buffer), "что", "быть", "весь", "этот", "один", "такой") > 5)
                    return russianEncoding;

                Encoding arabicEncoding = Encoding.GetEncoding(28596); // Arabic
                Encoding hewbrewEncoding = Encoding.GetEncoding(28598); // Hebrew
                if (GetCount(arabicEncoding.GetString(buffer), "من", "هل", "لا", "فى", "لقد", "ما") > 5)
                {
                    if (GetCount(hewbrewEncoding.GetString(buffer), "אולי", "אולי", "אולי", "אולי", "טוב", "טוב") > 10)
                        return hewbrewEncoding;
                    else
                        return arabicEncoding;
                }
                else if (GetCount(hewbrewEncoding.GetString(buffer), "אתה", "אולי", "הוא", "בסדר", "יודע", "טוב") > 5)
                    return hewbrewEncoding;

                return encoding;
            }
            catch
            {
                return Encoding.Default;
            }
        }

        public static string DictionaryFolder
        {
            get
            {
                return Path.GetDirectoryName(Application.ExecutablePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "Dictionaries" + Path.DirectorySeparatorChar;
            }
        }

        public static List<string> GetDictionaryLanguages()
        {
            var list = new List<string>();
            string dictionaryFolder = DictionaryFolder;
            if (Directory.Exists(dictionaryFolder))
            {
                foreach (string dic in Directory.GetFiles(dictionaryFolder, "*.dic"))
                {
                    string name = Path.GetFileNameWithoutExtension(dic);
                    if (!name.StartsWith("hyph"))
                    {
                        try
                        {
                            var ci = new CultureInfo(name.Replace("_", "-"));
                            name = ci.DisplayName + " [" + name + "]";
                        }
                        catch (Exception exception)
                        {
                            System.Diagnostics.Debug.WriteLine(exception.Message);
                            name = "[" + name + "]";
                        }
                        list.Add(name);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Get suggested display time in milliseconds (a tiny bit low).
        /// </summary>
        /// <param name="text">Subtitle text paragraph</param>
        /// <returns>Suggested display time in milliseconds (a tiny bit low).</returns>
        public static double GetDisplayMillisecondsFromText(string text)
        {
            int l = text.Length;

            if (l < 8)
                return 800;
            if (l < 15)
                return 1000;
            if (l < 25)
                return 1300;
            if (l < 35)
                return 1500;
            if (l < 50)
                return 2000;
            if (l < 75)
                return 2500;
            if (l < 90)
                return 3000;
            return 3500;
        }

        private static int GetCount(string text,
                            string word1,
                            string word2,
                            string word3,
                            string word4,
                            string word5,
                            string word6)
        {
            var regEx1 = new Regex("\\b" + word1 + "\\b");
            var regEx2 = new Regex("\\b" + word2 + "\\b");
            var regEx3 = new Regex("\\b" + word3 + "\\b");
            var regEx4 = new Regex("\\b" + word4 + "\\b");
            var regEx5 = new Regex("\\b" + word5 + "\\b");
            var regEx6 = new Regex("\\b" + word6 + "\\b");
            int count = regEx1.Matches(text).Count;
            count += regEx2.Matches(text).Count;
            count += regEx3.Matches(text).Count;
            count += regEx4.Matches(text).Count;
            count += regEx5.Matches(text).Count;
            count += regEx6.Matches(text).Count;
            return count;
        }

        public static string AutoDetectGoogleLanguage(string text, int bestCount)
        {
            int count = GetCount(text, "we", "are", "and", "you", "your", "what");
            if (count > bestCount)
                return "en";

            count = GetCount(text, "vi", "er", "og", "jeg", "var", "men");
            if (count > bestCount)
            {
                int norwegianCount = GetCount(text, "ut", "deg", "meg", "merkelig", "mye", "spørre");
                if (norwegianCount < 2)
                    return "da";
            }

            count = GetCount(text, "vi", "er", "og", "jeg", "var", "men");
            if (count > bestCount)
            {
                int danishCount = GetCount(text, "siger", "dig", "mig", "mærkelig", "meget", "spørge");
                if (danishCount < 2)
                    return "no";
            }

            count = GetCount(text, "vi", "är", "och", "Jag", "inte", "för");
            if (count > bestCount)
                return "sv";

            count = GetCount(text, "el", "bien", "Vamos", "Hola", "casa", "con");
            if (count > bestCount)
            {
                int frenchWords = GetCount(text, "Cest", "cest", "pas", "vous", "pour", "suis"); // not spanish words
                if (frenchWords < 2)
                    return "es";
            }

            count = GetCount(text, "un", "vous", "avec", "pas", "ce", "une");
            if (count > bestCount)
            {
                int spanishWords = GetCount(text, "Hola", "nada", "Vamos", "pasa", "los", "como"); // not french words
                int italianWords = GetCount(text, "Cosa", "sono", "Grazie", "Buongiorno", "bene", "questo");

                if (spanishWords < 2 && italianWords < 2)
                    return "fr";
            }

            count = GetCount(text, "und", "auch", "sich", "bin", "hast", "möchte");
            if (count > bestCount)
                return "de";

            count = GetCount(text, "van", "het", "een", "Het", "mij", "zijn");
            if (count > bestCount)
                return "nl";

            count = GetCount(text, "Czy", "ale", "ty", "siê", "jest", "mnie");
            if (count > bestCount)
                return "pl";

            count = GetCount(text, "Cosa", "sono", "Grazie", "Buongiorno", "bene", "questo");
            if (count > bestCount)
            {
                int frenchWords = GetCount(text, "Cest", "cest", "pas", "vous", "pour", "suis"); // not spanish words
                int spanishWords = GetCount(text, "Hola", "nada", "Vamos", "pasa", "los", "como"); // not french words
                if (frenchWords < 2 && spanishWords < 2)
                    return "it";
            }

            count = GetCount(text, "não", "Não", "Estás", "Então", "isso", "com");
            if (count > bestCount)
                return "pt-PT"; // PORTUGUESE

            count = GetCount(text, "μου", "είναι", "Είναι", "αυτό", "Τόμπυ", "καλά");
            if (count > bestCount)
                return "el"; // Greek

            count = GetCount(text, "все", "это", "как", "Воробей", "сюда", "Давай");
            if (count > bestCount)
                return "ru"; // Russian

            count = GetCount(text, "sam", "öto", "äto", "ovo", "vas", "što");
            if (count > bestCount && GetCount(text, "htjeti ", "htjeti ", "htjeti ", "htjeti ", "htjeti ", "htjeti ") > 0)
                return "hr"; // Croatia

            count = GetCount(text, "من", "هل", "لا", "فى", "لقد", "ما");
            if (count > bestCount)
            {
                if (GetCount(text, "אולי", "אולי", "אולי", "אולי", "טוב", "טוב") > 10)
                    return "he";
                return "ar"; // Arabic
            }

            count = GetCount(text, "אתה", "אולי", "הוא", "בסדר", "יודע", "טוב");
            if (count > bestCount)
                return "he"; // Hebrew

            count = GetCount(text, "sam", "što", "nije", "Šta", "ovde", "za");
            if (count > bestCount)
                return "sr"; // Serbian

            count = GetCount(text, "không", "tôi", "anh", "đó", "Tôi", "ông");
            if (count > bestCount)
                return "vi"; // Vietnamese

            return string.Empty;
        }

        public static string AutoDetectGoogleLanguage(Subtitle subtitle)
        {
            int bestCount = subtitle.Paragraphs.Count / 14;

            StringBuilder sb = new StringBuilder();
            foreach (Paragraph p in subtitle.Paragraphs)
                sb.AppendLine(p.Text);
            string text = sb.ToString();

            string languageId = AutoDetectGoogleLanguage(text, bestCount);

            if (string.IsNullOrEmpty(languageId))
                return "en";

            return languageId;
        }

        public static string AutoDetectLanguageName(string languageName, Subtitle subtitle)
        {
            int bestCount = subtitle.Paragraphs.Count / 14;

            StringBuilder sb = new StringBuilder();
            foreach (Paragraph p in subtitle.Paragraphs)
                sb.AppendLine(p.Text);
            string text = sb.ToString();

            foreach (string name in GetDictionaryLanguages())
            {
                string shortName = string.Empty;
                int start = name.IndexOf("[");
                int end = name.IndexOf("]");
                if (start > 0 && end > start)
                {
                    start++;
                    shortName = name.Substring(start, end - start);
                }

                int count;
                switch (shortName)
                {
                    case "da_DK":
                        count = GetCount(text, "vi", "er", "og", "jeg", "var", "men");
                        if (count > bestCount)
                        {
                            int norweigianCount = GetCount(text, "ut", "deg", "meg", "merkelig", "mye", "spørre");
                            if (norweigianCount < 2)
                                languageName = shortName;
                        }
                        break;
                    case "nb_NO":
                        count = GetCount(text, "vi", "er", "og", "jeg", "var", "men");
                        if (count > bestCount)
                        {
                            int danishCount = GetCount(text, "siger", "dig", "mig", "mærkelig", "meget", "spørge");
                            if (danishCount < 2)
                                languageName = shortName;
                        }
                        break;
                    case "en_US":
                        count = GetCount(text, "we", "are", "and", "you", "your", "what");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "sv_SE":
                        count = GetCount(text, "vi", "är", "och", "Jag", "inte", "för");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "es_ES":
                        count = GetCount(text, "el", "bien", "Vamos", "Hola", "casa", "con");
                        if (count > bestCount)
                        {
                            int frenchWords = GetCount(text, "Cest", "cest", "pas", "vous", "pour", "suis"); // not spanish words
                            if (frenchWords < 2)
                                languageName = shortName;
                        }
                        break;
                    case "fr_FR":
                        count = GetCount(text, "un", "vous", "avec", "pas", "ce", "une");
                        if (count > bestCount)
                        {
                            int spanishWords = GetCount(text, "Hola", "nada", "Vamos", "pasa", "los", "como"); // not french words
                            int italianWords = GetCount(text, "Cosa", "sono", "Grazie", "Buongiorno", "bene", "questo"); // not italian words
                            if (spanishWords < 2 && italianWords < 2)
                                languageName = shortName;
                        }
                        break;
                    case "it_IT":
                        count = GetCount(text, "Cosa", "sono", "Grazie", "Buongiorno", "bene", "questo");
                        if (count > bestCount)
                        {
                            int frenchWords = GetCount(text, "Cest", "cest", "pas", "vous", "pour", "suis"); // not spanish words
                            int spanishWords = GetCount(text, "Hola", "nada", "Vamos", "pasa", "los", "como"); // not french words
                            if (frenchWords < 2 && spanishWords < 2)
                                languageName = shortName;
                        }
                        break;
                    case "de_DE":
                        count = GetCount(text, "und", "auch", "sich", "bin", "hast", "möchte");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "nl_NL":
                        count = GetCount(text, "van", "het", "een", "Het", "mij", "zijn");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "pl_PL":
                        count = GetCount(text, "Czy", "ale", "ty", "siê", "jest", "mnie");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "el_GR":
                        count = GetCount(text, "μου", "είναι", "Είναι", "αυτό", "Τόμπυ", "καλά");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "ru_RU":
                        count = GetCount(text, "все", "это", "как", "Воробей", "сюда", "Давай");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "ro_RO":
                        count = GetCount(text, "sunt", "fost", "Bine", "Haide", "Trebuie", "trebuie");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "hr_HR": // Croatia
                        count = GetCount(text, "sam", "öto", "äto", "ovo", "vas", "što");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "pt_PT": // Portuguese
                        count = GetCount(text, "não", "Não", "Estás", "Então", "isso", "com");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    case "pt_BR": // Portuguese (Brasil)
                        count = GetCount(text, "não", "Não", "Estás", "Então", "isso", "com");
                        if (count > bestCount)
                            languageName = shortName;
                        break;
                    default:
                        break;
                }
            }
            return languageName;
        }

        public static string ColorToHex(System.Drawing.Color c)
        {
            string result = string.Format("#{0:x2}{1:x2}{2:x2}", c.R, c.G, c.B);
            return result;
        }

        public static int GetMaxLineLength(string text)
        {
            int maxLength = 0;
            foreach (string line in text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                string s = RemoveHtmlTags(line);
                if (s.Length > maxLength)
                    maxLength = s.Length;
            }
            return maxLength;
        }

        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        public static void ShowHelp(string parameter)
        {
            string helpFile = Configuration.Settings.Language.General.HelpFile;
            if (string.IsNullOrEmpty(helpFile))
                helpFile = "http://www.nikse.dk/se/Help.aspx";
            System.Diagnostics.Process.Start(helpFile + parameter);
        }

        public static string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public static string GetOpenDialogFiler()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Configuration.Settings.Language.General.SubtitleFiles + "|");
            foreach (SubtitleFormat s in SubtitleFormat.AllSubtitleFormats)
            {
                sb.Append("*" + s.Extension + ";");
                foreach (string ext in s.AlternateExtensions)
                {
                    sb.Append("*" + ext + ";");
                }
            }
            sb.Append("|" + Configuration.Settings.Language.General.AllFiles + "|*.*");
            return sb.ToString();
        }

        public static void SetSaveDialogFilter(SaveFileDialog saveFileDialog, SubtitleFormat currentFormat)
        {
            var sb = new StringBuilder();
            int index = 0;
            foreach (SubtitleFormat format in SubtitleFormat.AllSubtitleFormats)
            {
                sb.Append(format.FriendlyName + "|*" + format.Extension + "|");
                if (currentFormat.FriendlyName == format.FriendlyName)
                    saveFileDialog.FilterIndex = index + 1;
                index++;
            }
            saveFileDialog.Filter = sb.ToString().TrimEnd('|');
        }

        public static bool IsQuartsDllInstalled
        {
            get
            {
                if (IsRunningOnMono())
                    return false;

                string quartsInteropFileName = Path.GetDirectoryName(Application.ExecutablePath).TrimEnd('\\') + @"\Interop.QuartzTypeLib.dll";
                if (!File.Exists(quartsInteropFileName))
                    return false;

                string quartzFileName = Environment.GetFolderPath(Environment.SpecialFolder.System).TrimEnd('\\') + @"\quartz.dll";
                return File.Exists(quartzFileName);
            }
        }

        public static bool IsManagedDirectXInstalled
        {
            get
            {
                if (IsRunningOnMono())
                    return false;

                try
                {
                    //Check if this folder exists: C:\WINDOWS\Microsoft.NET\DirectX for Managed Code
                    string folderName = Environment.SystemDirectory.TrimEnd('\\');
                    folderName = folderName.Substring(0, folderName.LastIndexOf('\\'));
                    folderName = folderName + @"\\Microsoft.NET\DirectX for Managed Code";
                    return Directory.Exists(folderName);
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
            }
        }

        public static bool IsWmpAvailable
        {
            get
            {
                if (IsRunningOnMono())
                    return false;

                string wmpInteropFileName = Path.GetDirectoryName(Application.ExecutablePath).TrimEnd('\\') + @"\AxInterop.WMPLib.dll";
                if (!File.Exists(wmpInteropFileName))
                    return false;

                string wmpLibFileName = Path.GetDirectoryName(Application.ExecutablePath).TrimEnd('\\') + @"\Interop.WMPLib.dll";
                return File.Exists(wmpLibFileName);
            }
        }

        public static VideoPlayer GetVideoPlayer()
        {
            GeneralSettings gs = Configuration.Settings.General;

            if (gs.VideoPlayer == "VLC" && LibVlc11xDynamic.IsInstalled)
                return new LibVlc11xDynamic();
            if (gs.VideoPlayer == "WindowsMediaPlayer" && IsWmpAvailable)
                return new WmpPlayer();
            //if (gs.VideoPlayer == "ManagedDirectX" && IsManagedDirectXInstalled)
            //    return new ManagedDirectXPlayer();
            if (IsQuartsDllInstalled)
                return new QuartsPlayer();
            if (IsWmpAvailable)
                return new WmpPlayer();

            throw new NotSupportedException("You need DirectX or VLC media player 1.1.x installed as well as Subtitle Edit dll files!");
        }

        public static void InitializeVideoPlayerAndContainer(string fileName, VideoInfo videoInfo, VideoPlayerContainer videoPlayerContainer, EventHandler onVideoLoaded, EventHandler onVideoEnded)
        {
            try
            {
                videoPlayerContainer.VideoPlayer = GetVideoPlayer();
                videoPlayerContainer.VideoPlayer.Initialize(videoPlayerContainer.PanelPlayer, fileName, onVideoLoaded, onVideoEnded);
                videoPlayerContainer.ShowStopButton = Configuration.Settings.General.VideoPlayerShowStopButton;
                videoPlayerContainer.Volume = Configuration.Settings.General.VideoPlayerDefaultVolume;
            }
            catch (Exception exception)
            {
                videoPlayerContainer.VideoPlayer = null;
                var videoError = new VideoError();
                videoError.Initialize(fileName, videoInfo, exception);
                videoError.ShowDialog();
            }
        }


        internal static void DisplayLineLengths(Panel panelSingleLine, string text)
        {
            string cleanText = RemoveHtmlTags(text).Replace(Environment.NewLine, "|");
            string[] lines = cleanText.Split('|');

            int position = 0;

            // we must dispose before clearing controls (or this will occur: "Error creating window handle")
            foreach (Control ctrl in panelSingleLine.Controls)
                ctrl.Dispose();
            panelSingleLine.Controls.Clear();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i > 0)
                {
                    var labelSlash = new Label {AutoSize = true, Margin = new Padding(0)};
                    panelSingleLine.Controls.Add(labelSlash);
                    labelSlash.Text = "/";
                    labelSlash.Top = 0;
                    labelSlash.Left = position;
                    position += labelSlash.Width - 4;

                }
                var labelLength = new Label();
                labelLength.AutoSize = true;
                labelLength.Margin = new Padding(0);
                panelSingleLine.Controls.Add(labelLength);
                labelLength.Text = line.Length.ToString();
                labelLength.Top = 0;
                labelLength.Left = position;
                position += labelLength.Width - 4;
                if (line.Length > Configuration.Settings.General.SubtitleLineMaximumLength)
                    labelLength.ForeColor = System.Drawing.Color.Red;
                else if (line.Length > Configuration.Settings.General.SubtitleLineMaximumLength - 5)
                    labelLength.ForeColor = System.Drawing.Color.Orange;
            }
        }

        public static bool IsValidRegex(string testPattern)
        {
            if (!string.IsNullOrEmpty(testPattern))
            {
                try
                {
                    Regex.Match("", testPattern);
                    return true;
                }
                catch (ArgumentException)
                { // BAD PATTERN: Syntax error                 
                }
            }
            return false;
        }

        public static Regex MakeWordSearchRegex(string word)
        {
            string s = word.Replace("\\", "\\\\");
            s = s.Replace("*", "\\*");
            s = s.Replace(".", "\\.");
            s = s.Replace("?", "\\?");
            return new Regex(@"\b" + s + @"\b", RegexOptions.Compiled);
        }

        public static Regex MakeWordSearchRegexWithNumbers(string word)
        {
            string s = word.Replace("\\", "\\\\");
            s = s.Replace("*", "\\*");
            s = s.Replace(".", "\\.");
            s = s.Replace("?", "\\?");
            return new Regex(@"[\b ,\.\?\!]" + s + @"[\b !\.,\r\n\?]", RegexOptions.Compiled);
        }

        public static void AddToUserDictionary(string word, string languageName)
        {
            word = word.Trim();
            if (word.Length > 1)
            {
                string userWordsXmlFileName = DictionaryFolder + languageName + "_user.xml";
                var userWords = new XmlDocument();
                if (File.Exists(userWordsXmlFileName))
                    userWords.Load(userWordsXmlFileName);
                else
                    userWords.LoadXml("<words />");

                XmlNode node = userWords.CreateElement("word");
                node.InnerText = word;
                userWords.DocumentElement.AppendChild(node);
                userWords.Save(userWordsXmlFileName);
            }
        }

        public static bool AddWordToLocalNamesEtcList(string word, string languageName)
        {
            word = word.Trim();
            if (word.Length > 1)
            {
                var localNamesEtc = new List<string>();            
                string userNamesEtcXmlFileName = LoadLocalNamesEtc(localNamesEtc, localNamesEtc, languageName);

                if (localNamesEtc.Contains(word))
                    return false;
                localNamesEtc.Add(word);
                localNamesEtc.Sort();

                var namesEtcDoc = new XmlDocument();
                if (File.Exists(userNamesEtcXmlFileName))
                    namesEtcDoc.Load(userNamesEtcXmlFileName);
                else
                    namesEtcDoc.LoadXml("<ignore_words />");

                XmlNode de = namesEtcDoc.DocumentElement;
                if (de != null)
                {
                    de.RemoveAll();
                    foreach (var name in localNamesEtc)
                    {
                        XmlNode node = namesEtcDoc.CreateElement("name");
                        node.InnerText = name;
                        de.AppendChild(node);                        
                    }
                    namesEtcDoc.Save(userNamesEtcXmlFileName);
                }
                return true;
            }
            return false;
        }        

        public static string LoadNamesEtcWordLists(List<string> namesEtcList, List<string> namesEtcMultiWordList, string languageName)
        {
            namesEtcList.Clear();
            namesEtcMultiWordList.Clear();

            LoadGlobalNamesEtc(namesEtcList, namesEtcMultiWordList);

            string userNamesEtcXmlFileName = LoadLocalNamesEtc(namesEtcList, namesEtcMultiWordList, languageName);
            return userNamesEtcXmlFileName;
        }

        internal static void LoadGlobalNamesEtc(List<string> namesEtcList, List<string> namesEtcMultiWordList)
        {
            // Load names etc list (names/noise words)
            var namesEtcDoc = new XmlDocument();
            bool loaded = false;
            if (Configuration.Settings.WordLists.UseOnlineNamesEtc && !string.IsNullOrEmpty(Configuration.Settings.WordLists.NamesEtcUrl))
            {
                try
                {
                    string xml = ReadTextFileViaUrlAndProxyIfAvailable(Configuration.Settings.WordLists.NamesEtcUrl);
                    namesEtcDoc.LoadXml(xml);
                    loaded = true;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message + Environment.NewLine + exception.StackTrace);
                }
            }
            if (!loaded && File.Exists(DictionaryFolder + "names_etc.xml"))
            {
                namesEtcDoc.Load(DictionaryFolder + "names_etc.xml");
            }
            if (namesEtcDoc.DocumentElement != null)
                foreach (XmlNode node in namesEtcDoc.DocumentElement.SelectNodes("name"))
                {
                    string s = node.InnerText.Trim();
                    if (s.Contains(" "))
                    {
                        if (!namesEtcMultiWordList.Contains(s))
                            namesEtcMultiWordList.Add(s);
                    }
                    else
                    {
                        if (!namesEtcList.Contains(s))
                            namesEtcList.Add(s);
                    }
                }
        }

        internal static string LoadLocalNamesEtc(List<string> namesEtcList, List<string> namesEtcMultiWordList, string languageName)
        {
            string userNamesEtcXmlFileName = DictionaryFolder + languageName + "_names_etc.xml";
            if (!File.Exists(userNamesEtcXmlFileName) && languageName.Length == 2)
            {
                string[] files = Directory.GetFiles(DictionaryFolder, languageName + "_??_names_etc.xml");
                if (files.Length > 0)
                    userNamesEtcXmlFileName = files[0];
            }

            if (File.Exists(userNamesEtcXmlFileName))
            {
                var namesEtcDoc = new XmlDocument();
                namesEtcDoc.Load(userNamesEtcXmlFileName);
                foreach (XmlNode node in namesEtcDoc.DocumentElement.SelectNodes("name"))
                {
                    string s = node.InnerText.Trim();
                    if (s.Contains(" "))
                    {
                        if (!namesEtcMultiWordList.Contains(s))
                            namesEtcMultiWordList.Add(s);
                    }
                    else
                    {
                        if (!namesEtcList.Contains(s))
                            namesEtcList.Add(s);
                    }
                }
            }
            return userNamesEtcXmlFileName;
        }

        internal static bool IsInNamesEtcMultiWordList(List<string> namesEtcMultiWordList, string line, string word)
        {
            string text = line.Replace(Environment.NewLine, " ");
            text = text.Replace("  ", " ");

            foreach (string s in namesEtcMultiWordList)
            {
                if (s.Contains(word) && text.Contains(s))
                {
                    if (s.StartsWith(word + " ") || s.EndsWith(" " + word) || s.Contains(" " + word + " "))
                        return true;
                }
            }
            return false;
        }

        public static string LoadUserWordList(List<string> userWordList, string languageName)
        {
            userWordList.Clear();
            var userWordDictionary = new XmlDocument();
            string userWordListXmlFileName = DictionaryFolder + languageName + "_user.xml";
            if (File.Exists(userWordListXmlFileName))
            {
                userWordDictionary.Load(userWordListXmlFileName);
                foreach (XmlNode node in userWordDictionary.DocumentElement.SelectNodes("word"))
                {
                    string s = node.InnerText.ToLower();
                    if (!userWordList.Contains(s))
                        userWordList.Add(s);
                }
            }
            return userWordListXmlFileName;
        }

        public static string GetLetters(bool uppercase, bool lowercase, bool numbers)
        {
            string s = string.Empty;

            if (uppercase)
                s += Configuration.Settings.General.UppercaseLetters; 

            if (lowercase)
                s += Configuration.Settings.General.UppercaseLetters.ToLower();

            if (numbers)
                s += "0123456789";

            return s;
        }

    }
}