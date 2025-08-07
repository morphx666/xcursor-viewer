using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eto.Drawing;
using static xcursor_viewer.X11Structs;

namespace xcursor_viewer;

internal class XCursor {
    public enum CommentTypes {
        Copyright = (int)XCURSOR_COMMENT_COPYRIGHT,
        License = (int)XCURSOR_COMMENT_LICENSE,
        Other = (int)XCURSOR_COMMENT_OTHER
    }

    public X11CursorHeader Header { get; set; }
    public List<List<Bitmap>> Images { get; set; } = [];
    public List<(string, CommentTypes)> Comments { get; set; } = [];
    public List<X11Comment> CommentsChunks { get; set; } = [];
    public List<X11Image> ImagesChunks { get; set; } = [];
    public string Name { get; }

    public XCursor(string fileName) {
        List<Bitmap> tmpImages = [];

        Name = Path.GetFileNameWithoutExtension(fileName);

        using(FileStream fs = new(fileName, FileMode.Open, FileAccess.Read)) {
            using(BinaryReader br = new(fs)) {
                X11CursorHeader header = new() {
                    Magic = br.ReadUInt32(),
                    Size = br.ReadUInt32(),
                    Version = br.ReadUInt32(),
                    TableOfContentsCount = br.ReadUInt32()
                };
                header.TableOfContents = new X11CursorTableOfContents[header.TableOfContentsCount];

                for(int i = 0; i < header.TableOfContentsCount; i++) {
                    X11CursorTableOfContents toc = new() {
                        Type = br.ReadUInt32(),
                        SubType = br.ReadUInt32(),
                        Position = br.ReadUInt32()
                    };
                    header.TableOfContents[i] = toc;

                    long currentPosition = fs.Position;
                    fs.Seek(toc.Position, SeekOrigin.Begin);
                    switch(toc.Type) {
                        case XCURSOR_COMMENT_TYPE: // Comment
                            X11Comment commentChunk = new() {
                                Chunk = new() {
                                    Length = br.ReadUInt32(),
                                    Type = br.ReadUInt32(),
                                    SubType = br.ReadUInt32(),
                                    Version = br.ReadUInt32()
                                },
                                Length = br.ReadUInt32()
                            };
                            commentChunk.Comment = new string(br.ReadChars((int)commentChunk.Length));
                            Comments.Add((commentChunk.Comment, (CommentTypes)commentChunk.Chunk.Type));
                            CommentsChunks.Add(commentChunk);
                            break;
                        case XCURSOR_IMAGE_TYPE: // Image
                            X11Image imageChunk = new() {
                                Chunk = new() {
                                    Length = br.ReadUInt32(),
                                    Type = br.ReadUInt32(),
                                    SubType = br.ReadUInt32(),
                                    Version = br.ReadUInt32()
                                },
                                Width = br.ReadUInt32(), // TODO: Add constrain check: < 0x7fff
                                Height = br.ReadUInt32(), // TODO: Add constrain check: < 0x7fff
                                XHot = br.ReadUInt32(), // TODO: Add constrain check: < Width
                                YHot = br.ReadUInt32(), // TODO: Add constrain check: < Height
                                Delay = br.ReadUInt32()
                            };
                            imageChunk.Pixels = new UInt32[imageChunk.Width * imageChunk.Height];
                            ImagesChunks.Add(imageChunk);

                            UInt32 w = imageChunk.Width;
                            UInt32 h = imageChunk.Height;
                            Bitmap bitmap = new((int)w, (int)h, PixelFormat.Format32bppRgba);
                            using(BitmapData bd = bitmap.Lock()) {
                                for(int y = 0; y < h; y++) {
                                    for(int x = 0; x < w; x++) {
                                        UInt32 pixel = br.ReadUInt32();
                                        imageChunk.Pixels[x * y] = pixel;
                                        if(pixel != 0) bd.SetPixel(x, y, Color.FromArgb((int)pixel));
                                    }
                                }
                            }
                            tmpImages.Add(bitmap);
                            break;
                    }
                    fs.Seek(currentPosition, SeekOrigin.Begin);
                }

                for(int j = 0; j < tmpImages.Count; j++) {
                    UInt32 nominalSize = ImagesChunks[j].Chunk.SubType;
                    for(int k = 0; k < ImagesChunks.Count; k++) {
                        if(ImagesChunks[k].Chunk.SubType == nominalSize) {
                            if(ImagesChunks[k].Chunk.SubType != nominalSize) Debugger.Break();

                            while(Images.Count <= k) Images.Add([]);
                            Images[k].Add(tmpImages[j]);
                            break;
                        }
                    }
                }
                //foreach(var image in Images) image.Reverse();

                Header = header;
            }
        }
    }

    public static bool IsXCursor(string fileName) {
        try {
            using FileStream fs = new(fileName, FileMode.Open, FileAccess.Read);
            using BinaryReader br = new(fs);
            byte[] b = br.ReadBytes(4);
            if(b.Length < 4) return false;
            Array.Reverse(b);
            UInt32 magic = BitConverter.ToUInt32(b, 0);

            return magic == XCURSOR_MAGIC;
        } catch {
            return false;
        }
    }
}