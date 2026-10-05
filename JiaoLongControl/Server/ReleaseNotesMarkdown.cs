using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdBlock = Markdig.Syntax.Block;

namespace JiaoLongControl.Server
{
    /// <summary>
    /// GitHub 更新日志 Markdown → WPF FlowDocument 轻量渲染器 (基于 Markdig)。
    /// 覆盖发布说明常见元素: 标题/段落/粗斜体/删除线/行内与围栏代码/有序无序列表/
    /// 链接(点击用系统浏览器打开)/引用/分隔线/表格; 内嵌 HTML 标签与图片跳过,
    /// 图片以替代文本代替。
    /// </summary>
    public static class ReleaseNotesMarkdown
    {
        private const string BodyFont = "Microsoft YaHei UI, Segoe UI";
        private const string MonoFont = "Consolas, Microsoft YaHei UI";

        private static readonly SolidColorBrush TextBrush = FromRgb(0x44, 0x44, 0x44);
        private static readonly SolidColorBrush HeadingBrush = FromRgb(0x22, 0x22, 0x22);
        private static readonly SolidColorBrush LinkBrush = FromRgb(0x4A, 0x90, 0xD9);
        private static readonly SolidColorBrush QuoteBrush = FromRgb(0x77, 0x77, 0x77);
        private static readonly SolidColorBrush CodeBrush = FromRgb(0x33, 0x33, 0x33);
        private static readonly SolidColorBrush InlineCodeBackground = FromRgb(0xEF, 0xEF, 0xEF);
        private static readonly SolidColorBrush CodeBlockBackground = FromRgb(0xF6, 0xF8, 0xFA);
        private static readonly SolidColorBrush HeaderBackground = FromRgb(0xF5, 0xF5, 0xF5);
        private static readonly SolidColorBrush LineBrush = FromRgb(0xE0, 0xE0, 0xE0);

        /// <summary>渲染 Markdown 为 FlowDocument, 已挂接超链接点击打开浏览器</summary>
        public static FlowDocument ToFlowDocument(string markdown)
        {
            // GitHub 发布说明中单个换行按硬换行渲染 (与 GitHub 页面行为一致)
            var pipeline = new MarkdownPipelineBuilder()
                .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
                .UsePipeTables()
                .UseAutoLinks()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();

            var document = CreateDocument();
            document.AddHandler(
                Hyperlink.RequestNavigateEvent,
                new RequestNavigateEventHandler(OnLinkNavigate));

            var markdownDocument = Markdown.Parse(markdown, pipeline);
            var renderer = new Renderer(document);
            renderer.RenderBlocks(markdownDocument, document.Blocks, inQuote: false);
            return document;
        }

        /// <summary>纯文本兜底 (无更新日志时的提示)</summary>
        public static FlowDocument PlainText(string text)
        {
            var document = CreateDocument();
            document.Blocks.Add(new Paragraph(new Run(text)));
            return document;
        }

        private static FlowDocument CreateDocument()
        {
            return new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily = new FontFamily(BodyFont),
                FontSize = 13,
                LineHeight = 20,
                Foreground = TextBrush,
            };
        }

        private static void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            if (e.Uri.Scheme is not ("http" or "https" or "mailto")) return;
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
            }
            catch
            {
                // 无法调起浏览器时保持静默
            }
        }

        private static SolidColorBrush FromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private sealed class Renderer
        {
            public Renderer(FlowDocument document) => Document = document;

            public FlowDocument Document { get; }

            public void RenderBlocks(IEnumerable<MdBlock> blocks, BlockCollection target, bool inQuote)
            {
                foreach (var block in blocks)
                {
                    switch (block)
                    {
                        case HeadingBlock heading:
                            var head = new Paragraph { Foreground = HeadingBrush };
                            RenderInlines(heading.Inline, head.Inlines, inQuote);
                            (head.FontSize, head.FontWeight, head.Margin) = heading.Level switch
                            {
                                1 => (20.0, FontWeights.Bold, new Thickness(0, 4, 0, 8)),
                                2 => (18.0, FontWeights.Bold, new Thickness(0, 4, 0, 7)),
                                3 => (16.0, FontWeights.Bold, new Thickness(0, 3, 0, 6)),
                                4 => (14.0, FontWeights.Bold, new Thickness(0, 3, 0, 5)),
                                _ => (13.0, FontWeights.Bold, new Thickness(0, 3, 0, 4)),
                            };
                            target.Add(head);
                            break;

                        case ParagraphBlock paragraph:
                            var para = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
                            RenderInlines(paragraph.Inline, para.Inlines, inQuote);
                            target.Add(para);
                            break;

                        case ListBlock list:
                            target.Add(RenderList(list, inQuote));
                            break;

                        case QuoteBlock quote:
                            // Section 边框在部分宿主下不渲染, 缩进 + 灰字保证引用可辨识
                            var section = new Section
                            {
                                Margin = new Thickness(0, 4, 0, 10),
                                Padding = new Thickness(10, 2, 0, 2),
                                BorderBrush = LineBrush,
                                BorderThickness = new Thickness(3, 0, 0, 0),
                                Foreground = QuoteBrush,
                            };
                            RenderBlocks(quote, section.Blocks, inQuote: true);
                            target.Add(section);
                            break;

                        case CodeBlock code: // 含 FencedCodeBlock; Lines 为 LeafBlock 公共字段
                            var codeText = (code.Lines.Count > 0 ? code.Lines.ToString() : "")
                                .Replace("\r\n", "\n").Replace('\r', '\n')
                                .TrimEnd('\n');
                            var codePara = new Paragraph
                            {
                                Margin = new Thickness(0, 2, 0, 8),
                                Padding = new Thickness(8, 6, 8, 6),
                                Background = CodeBlockBackground,
                                Foreground = CodeBrush,
                                FontFamily = new FontFamily(MonoFont),
                                FontSize = 12,
                                LineHeight = 17,
                                BorderBrush = LineBrush,
                                BorderThickness = new Thickness(1),
                            };
                            codePara.Inlines.Add(new Run(codeText));
                            target.Add(codePara);
                            break;

                        case ThematicBreakBlock:
                            target.Add(new Paragraph
                            {
                                Margin = new Thickness(0, 8, 0, 8),
                                BorderBrush = LineBrush,
                                BorderThickness = new Thickness(0, 0, 0, 1),
                            });
                            break;

                        case MdTable table:
                            target.Add(RenderTable(table, inQuote));
                            break;

                            // HtmlBlock 等其余块类型跳过
                    }
                }
            }

            private List RenderList(ListBlock list, bool inQuote)
            {
                var wpfList = new List
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(0),
                    MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                };
                foreach (ListItemBlock item in list)
                {
                    var wpfItem = new ListItem { Padding = new Thickness(0) };
                    RenderBlocks(item, wpfItem.Blocks, inQuote);
                    wpfList.ListItems.Add(wpfItem);
                }
                return wpfList;
            }

            private Table RenderTable(MdTable table, bool inQuote)
            {
                int columnCount = Math.Max(1, table.ColumnDefinitions.Count);
                var wpfTable = new Table
                {
                    Margin = new Thickness(0, 2, 0, 8),
                    CellSpacing = 0,
                };
                for (int i = 0; i < columnCount; i++)
                    wpfTable.Columns.Add(new TableColumn());

                var group = new TableRowGroup();
                wpfTable.RowGroups.Add(group);
                foreach (MdTableRow row in table)
                {
                    var wpfRow = new TableRow();
                    group.Rows.Add(wpfRow);
                    foreach (MdTableCell cell in row)
                    {
                        var wpfCell = new TableCell
                        {
                            Padding = new Thickness(8, 4, 8, 4),
                            BorderBrush = LineBrush,
                            BorderThickness = new Thickness(0, 0, 0, 1),
                        };
                        if (row.IsHeader)
                        {
                            wpfCell.Background = HeaderBackground;
                            wpfCell.FontWeight = FontWeights.SemiBold;
                        }
                        RenderBlocks(cell, wpfCell.Blocks, inQuote);
                        wpfRow.Cells.Add(wpfCell);
                    }
                }
                return wpfTable;
            }

            private void RenderInlines(ContainerInline? container, InlineCollection target, bool inQuote)
            {
                if (container is null) return;
                foreach (var inline in container)
                {
                    switch (inline)
                    {
                        case AutolinkInline autolink:
                            // 邮箱自动链接补 mailto: 以支持点击打开
                            var navUrl = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
                            target.Add(MakeHyperlink(navUrl, null, autolink.Url, inQuote));
                            break;

                        case CodeInline code:
                            target.Add(new Run(code.Content)
                            {
                                FontFamily = new FontFamily(MonoFont),
                                FontSize = 12,
                                Foreground = CodeBrush,
                                Background = InlineCodeBackground,
                            });
                            break;

                        case LiteralInline literal:
                            target.Add(new Run(literal.Content.ToString()));
                            break;

                        case LineBreakInline:
                            target.Add(new LineBreak());
                            break;

                        case EmphasisInline emphasis:
                            AddEmphasis(emphasis, target, inQuote);
                            break;

                        case LinkInline link when !link.IsImage:
                            var linkUrl = link.Url ?? "";
                            target.Add(MakeHyperlink(linkUrl, link, linkUrl, inQuote));
                            break;

                        case LinkInline image:
                            // 图片 (README 徽章等) 以替代文本代替
                            foreach (var child in image)
                                if (child is LiteralInline alt)
                                    target.Add(new Run(alt.Content.ToString()));
                            break;

                        case HtmlEntityInline entity:
                            var raw = entity.Original.ToString();
                            if (string.IsNullOrEmpty(raw)) raw = entity.Transcoded.ToString();
                            target.Add(new Run(WebUtility.HtmlDecode(raw)));
                            break;

                        case HtmlInline: // 内嵌 HTML 标签, 跳过
                            break;

                        case ContainerInline nested:
                            RenderInlines(nested, target, inQuote);
                            break;

                        default:
                            target.Add(new Run(inline.ToString()));
                            break;
                    }
                }
            }

            private void AddEmphasis(EmphasisInline emphasis, InlineCollection target, bool inQuote)
            {
                bool isStar = emphasis.DelimiterChar is '*' or '_';
                bool bold = isStar && emphasis.DelimiterCount >= 2;
                bool italic = isStar && emphasis.DelimiterCount == 1;
                bool strike = emphasis.DelimiterChar is '~';

                var wrapper = new Span();
                if (strike)
                    wrapper.TextDecorations = TextDecorations.Strikethrough;

                Span inner = wrapper;
                if (bold)
                {
                    var b = new Bold();
                    inner.Inlines.Add(b);
                    inner = b;
                }
                if (italic)
                {
                    var i = new Italic();
                    inner.Inlines.Add(i);
                    inner = i;
                }

                RenderInlines(emphasis, inner.Inlines, inQuote);
                target.Add(wrapper);
            }

            private Hyperlink MakeHyperlink(string url, ContainerInline? content, string fallbackText, bool inQuote)
            {
                var hyperlink = new Hyperlink { Foreground = LinkBrush };
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
                    hyperlink.NavigateUri = uri;
                // 相对路径/锚点链接不设 NavigateUri, 仅作蓝色文本展示
                if (content is not null)
                    RenderInlines(content, hyperlink.Inlines, inQuote);
                if (hyperlink.Inlines.Count == 0)
                    hyperlink.Inlines.Add(new Run(fallbackText));
                return hyperlink;
            }
        }
    }
}
