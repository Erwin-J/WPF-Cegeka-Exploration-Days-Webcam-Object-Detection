﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using Vurdalakov;

namespace onnxruntime
{
    public static class ImageProcessor
    {
        public static string GetAbsolutePath(string relativePath)
        {
            FileInfo _dataRoot = new FileInfo(typeof(ImageProcessor).Assembly.Location);
            string assemblyFolderPath = _dataRoot.Directory.FullName;

            return Path.Combine(assemblyFolderPath, relativePath);
        }

        public static Stream Camera()
        {
            var video = VideoCapture.FromCamera(0);

            if (!video.IsOpened())
                throw new Exception("Camera not opened");

            Mat frame = new Mat();
            video.Read(frame);
            if (frame.Empty())
                throw new Exception("Frame is empty");

            return frame.ToMemoryStream();
        }

        public static System.Drawing.Bitmap DetectObjects(MemoryStream memoryStream)
        {
            var assetsRelativePath = @"../../../assets";
            string assetsPath = GetAbsolutePath(assetsRelativePath);
            var modelFilePath2 = Path.Combine(assetsPath, "Model", "FasterRCNN-10.onnx");

            // Read paths
            string modelFilePath = modelFilePath2;

            // Read image
            using Image<Rgb24> image = Image.Load<Rgb24>(memoryStream, out IImageFormat format);

            // Resize image
            float ratio = 800f / Math.Min(image.Width, image.Height);
            using Stream imageStream = new MemoryStream();
            image.Mutate(x => x.Resize((int)(ratio * image.Width), (int)(ratio * image.Height)));
            image.Save(imageStream, format);

            // Preprocess image
            var paddedHeight = (int)(Math.Ceiling(image.Height / 32f) * 32f);
            var paddedWidth = (int)(Math.Ceiling(image.Width / 32f) * 32f);
            Tensor<float> input = new DenseTensor<float>(new[] { 3, paddedHeight, paddedWidth });
            var mean = new[] { 102.9801f, 115.9465f, 122.7717f };
            for (int y = paddedHeight - image.Height; y < image.Height; y++)
            {
                Span<Rgb24> pixelSpan = image.GetPixelRowSpan(y);
                for (int x = paddedWidth - image.Width; x < image.Width; x++)
                {
                    input[0, y, x] = pixelSpan[x].B - mean[0];
                    input[1, y, x] = pixelSpan[x].G - mean[1];
                    input[2, y, x] = pixelSpan[x].R - mean[2];
                }
            }

            // Setup inputs and outputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image", input)
            };

            // Run inference
            using var session = new InferenceSession(modelFilePath);
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);

            // Postprocess to get predictions
            var resultsArray = results.ToArray();
            float[] boxes = resultsArray[0].AsEnumerable<float>().ToArray();
            long[] labels = resultsArray[1].AsEnumerable<long>().ToArray();
            float[] confidences = resultsArray[2].AsEnumerable<float>().ToArray();
            var predictions = new List<Prediction>();
            var minConfidence = 0.7f;
            for (int i = 0; i < boxes.Length - 4; i += 4)
            {
                var index = i / 4;
                if (confidences[index] >= minConfidence)
                {
                    predictions.Add(new Prediction
                    {
                        Box = new Box(boxes[i], boxes[i + 1], boxes[i + 2], boxes[i + 3]),
                        Label = LabelMap.Labels[labels[index]],
                        Confidence = confidences[index]
                    });
                }
            }

            // Put boxes, labels and confidence on image and save for viewing
            Font font = SystemFonts.CreateFont("Arial", 16);
            foreach (var p in predictions)
            {
                image.Mutate(x =>
                {
                    x.DrawLines(Color.Red, 2f, new PointF[] {

                        new PointF(p.Box.Xmin, p.Box.Ymin),
                        new PointF(p.Box.Xmax, p.Box.Ymin),

                        new PointF(p.Box.Xmax, p.Box.Ymin),
                        new PointF(p.Box.Xmax, p.Box.Ymax),

                        new PointF(p.Box.Xmax, p.Box.Ymax),
                        new PointF(p.Box.Xmin, p.Box.Ymax),

                        new PointF(p.Box.Xmin, p.Box.Ymax),
                        new PointF(p.Box.Xmin, p.Box.Ymin)
                    });
                    x.DrawText($"{p.Label}, {p.Confidence:0.00}", font, Color.White, new PointF(p.Box.Xmin, p.Box.Ymin));
                });
            }

            return image.ToBitmap();
        }
    }
}