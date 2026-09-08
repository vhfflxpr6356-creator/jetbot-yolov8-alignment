using Microsoft.ML.OnnxRuntime;
using System;
using System.IO;
using System.Linq;
using OpenCvSharp;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;

namespace SmartTrafficDashboard.Services
{

    public class Detection
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; }
    }
    public class YoloService : IDisposable
    {
        public float[] Run(
    Mat frame,
    out float scale,
    out int padLeft,
    out int padTop)
        {
            scale = 1.0f;
            padLeft = 0;
            padTop = 0;

            if (_session == null)
                return null;

            if (frame == null || frame.Empty())
                return null;

            const int inputSize = 640;

            // 원본 영상 비율 유지
            float scaleX = (float)inputSize / frame.Width;
            float scaleY = (float)inputSize / frame.Height;

            scale = Math.Min(scaleX, scaleY);

            int resizedWidth =
                (int)Math.Round(frame.Width * scale);

            int resizedHeight =
                (int)Math.Round(frame.Height * scale);

            // 중앙에 배치하기 위한 Padding 계산
            int totalPadX = inputSize - resizedWidth;
            int totalPadY = inputSize - resizedHeight;

            padLeft = totalPadX / 2;
            padTop = totalPadY / 2;

            int padRight =
                totalPadX - padLeft;

            int padBottom =
                totalPadY - padTop;

            using (Mat resized = new Mat())
            using (Mat letterbox = new Mat())
            using (Mat rgb = new Mat())
            {
                // 비율 유지 Resize
                Cv2.Resize(
                    frame,
                    resized,
                    new Size(
                        resizedWidth,
                        resizedHeight
                    )
                );

                // Ultralytics 계열 Letterbox의 일반적인 padding 값 = 114
                Cv2.CopyMakeBorder(
                    resized,
                    letterbox,
                    padTop,
                    padBottom,
                    padLeft,
                    padRight,
                    BorderTypes.Constant,
                    new Scalar(114, 114, 114)
                );

                // BGR → RGB
                Cv2.CvtColor(
                    letterbox,
                    rgb,
                    ColorConversionCodes.BGR2RGB
                );

                var tensor =
                    new DenseTensor<float>(
                        new[] { 1, 3, 640, 640 }
                    );

                for (int y = 0; y < inputSize; y++)
                {
                    for (int x = 0; x < inputSize; x++)
                    {
                        Vec3b pixel =
                            rgb.At<Vec3b>(y, x);

                        tensor[0, 0, y, x] =
                            pixel.Item0 / 255.0f;

                        tensor[0, 1, y, x] =
                            pixel.Item1 / 255.0f;

                        tensor[0, 2, y, x] =
                            pixel.Item2 / 255.0f;
                    }
                }

                var inputs =
                    new List<NamedOnnxValue>
                    {
                NamedOnnxValue.CreateFromTensor(
                    InputName,
                    tensor
                )
                    };

                using (var results = _session.Run(inputs))
                {
                    var output =
                        results
                        .First()
                        .AsTensor<float>();

                    return output.ToArray();
                }
            }
        }

        public List<Detection> Detect(Mat frame, float confidenceThreshold = 0.50f)
        {
            List<Detection> detections = new List<Detection>();

            float scale;
            int padLeft;
            int padTop;

            float[] output = Run(
                frame,
                out scale,
                out padLeft,
                out padTop
            );

            // [1, 6, 8400] = 50400
            if (output == null || output.Length != 50400)
                return detections;

            const int predictionCount = 8400;

            for (int i = 0; i < predictionCount; i++)
            {
                float centerX =
                    output[0 * predictionCount + i];

                float centerY =
                    output[1 * predictionCount + i];

                float width =
                    output[2 * predictionCount + i];

                float height =
                    output[3 * predictionCount + i];

                // Letterbox 좌표 → 원본 카메라 좌표로 복원
                centerX =
                    (centerX - padLeft) / scale;

                centerY =
                    (centerY - padTop) / scale;

                width =
                    width / scale;

                height =
                    height / scale;

                float ambulanceConfidence =
                    output[4 * predictionCount + i];

                float jetbotConfidence =
                    output[5 * predictionCount + i];

                int classId;
                float confidence;

                if (ambulanceConfidence > jetbotConfidence)
                {
                    classId = 0;
                    confidence = ambulanceConfidence;
                }
                else
                {
                    classId = 1;
                    confidence = jetbotConfidence;
                }

                if (confidence < confidenceThreshold)
                    continue;

                detections.Add(new Detection
                {
                    X = centerX,
                    Y = centerY,
                    Width = width,
                    Height = height,
                    Confidence = confidence,
                    ClassId = classId
                });
            }

            return ApplyNms(detections, 0.45f);
        }

        private List<Detection> ApplyNms(
    List<Detection> detections,
    float iouThreshold)
        {
            List<Detection> result = new List<Detection>();

            var sortedDetections = detections
                .OrderByDescending(d => d.Confidence)
                .ToList();

            while (sortedDetections.Count > 0)
            {
                Detection best = sortedDetections[0];

                result.Add(best);

                sortedDetections.RemoveAt(0);

                sortedDetections = sortedDetections
                 .Where(d =>
                  d.ClassId != best.ClassId ||
                  CalculateIoU(best, d) < iouThreshold
                 )
                 .ToList();
            }

            return result;
        }

        private float CalculateIoU(Detection a, Detection b)
        {
            float aLeft = a.X - a.Width / 2f;
            float aTop = a.Y - a.Height / 2f;
            float aRight = a.X + a.Width / 2f;
            float aBottom = a.Y + a.Height / 2f;

            float bLeft = b.X - b.Width / 2f;
            float bTop = b.Y - b.Height / 2f;
            float bRight = b.X + b.Width / 2f;
            float bBottom = b.Y + b.Height / 2f;

            float intersectionLeft = Math.Max(aLeft, bLeft);
            float intersectionTop = Math.Max(aTop, bTop);
            float intersectionRight = Math.Min(aRight, bRight);
            float intersectionBottom = Math.Min(aBottom, bBottom);

            float intersectionWidth =
                Math.Max(0, intersectionRight - intersectionLeft);

            float intersectionHeight =
                Math.Max(0, intersectionBottom - intersectionTop);

            float intersectionArea =
                intersectionWidth * intersectionHeight;

            float areaA = a.Width * a.Height;
            float areaB = b.Width * b.Height;

            float unionArea =
                areaA + areaB - intersectionArea;

            if (unionArea <= 0)
                return 0;

            return intersectionArea / unionArea;
        }

        private InferenceSession _session;

        public bool IsLoaded => _session != null;

        public string InputName { get; private set; }
        public string OutputName { get; private set; }

        public string LoadModel()
        {
            try
            {
                string modelPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "AIModels",
                    "best.onnx"
                );

                if (!File.Exists(modelPath))
                {
                    return "MODEL FILE NOT FOUND";
                }

                _session = new InferenceSession(modelPath);

                InputName = _session.InputMetadata.Keys.FirstOrDefault();
                OutputName = _session.OutputMetadata.Keys.FirstOrDefault();

                return "MODEL LOADED";
            }
            catch (Exception ex)
            {
                return "MODEL LOAD ERROR: " + ex.Message;
            }
        }

        public string GetModelInfo()
        {
            if (_session == null)
                return "MODEL NOT LOADED";

            string result = "";

            result += "=== INPUTS ===" + Environment.NewLine;

            foreach (var input in _session.InputMetadata)
            {
                result += input.Key
                       + " ["
                       + string.Join(", ", input.Value.Dimensions)
                       + "]"
                       + Environment.NewLine;
            }

            result += Environment.NewLine;
            result += "=== OUTPUTS ===" + Environment.NewLine;

            foreach (var output in _session.OutputMetadata)
            {
                result += output.Key
                       + " ["
                       + string.Join(", ", output.Value.Dimensions)
                       + "]"
                       + Environment.NewLine;
            }
            result += Environment.NewLine;
            result += "=== MODEL METADATA ===" + Environment.NewLine;

            foreach (var item in _session.ModelMetadata.CustomMetadataMap)
            {
                result += item.Key
                       + " = "
                       + item.Value
                       + Environment.NewLine;
            }

            return result;
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}