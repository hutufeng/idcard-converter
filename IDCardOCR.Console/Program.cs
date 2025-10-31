
using System;
using System.IO;
using Sdcb.PaddleOCR;

using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleInference;
using OpenCvSharp;
using System.Linq;

namespace IDCardOCR.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            string imagePath = "../test/A_ (1).jpg";

            if (!File.Exists(imagePath))
            {
                System.Console.WriteLine($"错误：找不到图像文件 '{imagePath}'。");
                return;
            }

            // 使用本地的中文V5模型
            FullOcrModel model = LocalFullModels.ChineseV5;

            // 初始化 PaddleOCR 引擎
            using (PaddleOcrAll engine = new PaddleOcrAll(model, PaddleDevice.Mkldnn()))
            {
                // 加载图像并执行 OCR
                using (Mat src = Cv2.ImRead(imagePath, ImreadModes.Color))
                {
                    PaddleOcrResult result = engine.Run(src);

                    // 打印结果
                    if (result.Regions.Count() > 0)
                    {
                        System.Console.WriteLine($"在 '{imagePath}' 中识别到的文本：");
                        foreach (var region in result.Regions)
                        {
                            System.Console.WriteLine(region.Text);
                        }
                    }
                    else
                    {
                        System.Console.WriteLine("未识别到任何文本。");
                    }
                }
            }
        }
    }
}
