// Run after the Play Mode captures. Read-only pixel checks; copies evidence outside Assets.
var destination = "Docs/Stage0/Screenshots";
System.IO.Directory.CreateDirectory(destination);
foreach(var path in System.IO.Directory.GetFiles("Assets/BattleCitySmoke", "*.png"))
{
    var name = System.IO.Path.GetFileName(path);
    if (!name.StartsWith("gallery-", System.StringComparison.Ordinal) && name != "baseline-256x240.png" && name != "baseline-1000x700.png") continue;
    System.IO.File.Copy(path, System.IO.Path.Combine(destination, name), true);
}
var native = new UnityEngine.Texture2D(2,2);
native.LoadImage(System.IO.File.ReadAllBytes(destination + "/baseline-256x240.png"));
if(native.width != 256 || native.height != 240) throw new System.Exception("Native capture resolution mismatch.");
var reference = native.GetPixels32();
var report = new System.Text.StringBuilder("M0 pixel comparison (RGBA byte-for-byte, live Game View screenshots)\n");
try
{
    foreach(var entry in new[] { "gallery-up-0.png", "baseline-1000x700.png" })
    {
        var image = new UnityEngine.Texture2D(2,2);
        try
        {
            image.LoadImage(System.IO.File.ReadAllBytes(destination + "/" + entry));
            int n = System.Math.Min(image.width / 256, image.height / 240);
            int left = (image.width - 256*n)/2, bottom = (image.height - 240*n)/2;
            var pixels = image.GetPixels32(); int differences = 0, invalidBars = 0;
            for(int y=0;y<image.height;y++) for(int x=0;x<image.width;x++)
            {
                var actual = pixels[y*image.width+x];
                if(x >= left && x < left+256*n && y >= bottom && y < bottom+240*n)
                {
                    var expected = reference[((y-bottom)/n)*256+(x-left)/n];
                    if(!actual.Equals(expected)) differences++;
                }
                else if(actual.r != 0 || actual.g != 0 || actual.b != 0 || actual.a != 255) invalidBars++;
            }
            report.AppendLine(entry+": "+image.width+"x"+image.height+", scale="+n+", content="+(256*n)+"x"+(240*n)+", offset="+left+","+bottom+", mismatchedContentPixels="+differences+", invalidBlackBarPixels="+invalidBars);
            if(differences > 0 || invalidBars > 0) throw new System.Exception(report.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }
}
finally { UnityEngine.Object.DestroyImmediate(native); }
System.IO.File.WriteAllText("Docs/Stage0/Visual-pixel-check.txt",report.ToString());
return report.ToString();
