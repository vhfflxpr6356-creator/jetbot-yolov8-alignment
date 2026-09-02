using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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
        private InferenceSession _session;

        public bool IsLoaded =>
            _session != null;

        public string InputName
        {
            get;
            private set;
        }

        public string OutputName
        {
            get;
            private set;
        }


        // =========================================================
        // YOLO 실행
        //
        // 입력:
        // 640 x 640
        //
        // 출력:
        // [1, 6, 8400]
        //
        // 전처리:
        // Resize
        // -> Letterbox
        // -> BGR to RGB
        // -> Float 0~1
        // -> HWC to CHW
        //
        // 기존 C# 픽셀별 이중 for문 제거
        // =========================================================
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
            {
                return null;
            }


            if (
                frame == null ||
                frame.Empty()
            )
            {
                return null;
            }


            const int inputSize =
                640;


            // =====================================================
            // Letterbox Scale 계산
            // =====================================================
            float scaleX =
                (float)inputSize /
                frame.Width;

            float scaleY =
                (float)inputSize /
                frame.Height;


            scale =
                Math.Min(
                    scaleX,
                    scaleY
                );


            int resizedWidth =
                (int)Math.Round(
                    frame.Width *
                    scale
                );


            int resizedHeight =
                (int)Math.Round(
                    frame.Height *
                    scale
                );


            int totalPadX =
                inputSize -
                resizedWidth;


            int totalPadY =
                inputSize -
                resizedHeight;


            padLeft =
                totalPadX / 2;


            padTop =
                totalPadY / 2;


            int padRight =
                totalPadX -
                padLeft;


            int padBottom =
                totalPadY -
                padTop;


            using (
                Mat resized =
                    new Mat()
            )
            using (
                Mat letterbox =
                    new Mat()
            )
            using (
                Mat rgb =
                    new Mat()
            )
            using (
                Mat floatImage =
                    new Mat()
            )
            {
                // =================================================
                // Resize
                // =================================================
                Cv2.Resize(
                    frame,
                    resized,
                    new Size(
                        resizedWidth,
                        resizedHeight
                    ),
                    0,
                    0,
                    InterpolationFlags.Linear
                );


                // =================================================
                // Letterbox
                //
                // Ultralytics 기본 Padding = 114
                // =================================================
                Cv2.CopyMakeBorder(
                    resized,
                    letterbox,
                    padTop,
                    padBottom,
                    padLeft,
                    padRight,
                    BorderTypes.Constant,
                    new Scalar(
                        114,
                        114,
                        114
                    )
                );


                // =================================================
                // BGR -> RGB
                // =================================================
                Cv2.CvtColor(
                    letterbox,
                    rgb,
                    ColorConversionCodes.BGR2RGB
                );


                // =================================================
                // byte 0~255
                // ->
                // float 0~1
                //
                // OpenCV 내부 연산 사용
                // =================================================
                rgb.ConvertTo(
                    floatImage,
                    MatType.CV_32FC3,
                    1.0 / 255.0
                );


                // =================================================
                // HWC -> CHW
                //
                // R Plane
                // G Plane
                // B Plane
                // =================================================
                Mat[] channels =
                    Cv2.Split(
                        floatImage
                    );


                try
                {
                    int planeSize =
                        inputSize *
                        inputSize;


                    float[] inputData =
                        new float[
                            planeSize *
                            3
                        ];


                    // =================================================
                    // R Channel
                    // =================================================
                    Marshal.Copy(
                        channels[0].Data,
                        inputData,
                        0,
                        planeSize
                    );


                    // =================================================
                    // G Channel
                    // =================================================
                    Marshal.Copy(
                        channels[1].Data,
                        inputData,
                        planeSize,
                        planeSize
                    );


                    // =================================================
                    // B Channel
                    // =================================================
                    Marshal.Copy(
                        channels[2].Data,
                        inputData,
                        planeSize * 2,
                        planeSize
                    );


                    // =================================================
                    // Tensor 생성
                    //
                    // [1, 3, 640, 640]
                    // =================================================
                    var tensor =
                        new DenseTensor<float>(
                            inputData,
                            new[]
                            {
                                1,
                                3,
                                inputSize,
                                inputSize
                            }
                        );


                    var inputs =
                        new List<
                            NamedOnnxValue
                        >
                        {
                            NamedOnnxValue
                                .CreateFromTensor(
                                    InputName,
                                    tensor
                                )
                        };


                    // =================================================
                    // OpenVINO 추론
                    // =================================================
                    using (
                        var results =
                            _session.Run(
                                inputs
                            )
                    )
                    {
                        Tensor<float> output =
                            results
                                .First()
                                .AsTensor<float>();


                        return
                            output.ToArray();
                    }
                }
                finally
                {
                    if (
                        channels != null
                    )
                    {
                        foreach (
                            Mat channel
                            in channels
                        )
                        {
                            channel?.Dispose();
                        }
                    }
                }
            }
        }


        // =========================================================
        // Detection
        //
        // output0:
        // [1, 6, 8400]
        //
        // 0 = Center X
        // 1 = Center Y
        // 2 = Width
        // 3 = Height
        // 4 = Ambulance Confidence
        // 5 = JetBot Confidence
        // =========================================================
        public List<Detection> Detect(
            Mat frame,
            float confidenceThreshold = 0.50f)
        {
            List<Detection> detections =
                new List<Detection>();


            float scale;

            int padLeft;

            int padTop;


            float[] output =
                Run(
                    frame,
                    out scale,
                    out padLeft,
                    out padTop
                );


            // =====================================================
            // [1, 6, 8400]
            //
            // 6 * 8400
            // =
            // 50400
            // =====================================================
            if (
                output == null ||
                output.Length != 50400
            )
            {
                return detections;
            }


            const int predictionCount =
                8400;


            for (
                int i = 0;
                i < predictionCount;
                i++
            )
            {
                float centerX =
                    output[
                        0 *
                        predictionCount +
                        i
                    ];


                float centerY =
                    output[
                        1 *
                        predictionCount +
                        i
                    ];


                float width =
                    output[
                        2 *
                        predictionCount +
                        i
                    ];


                float height =
                    output[
                        3 *
                        predictionCount +
                        i
                    ];


                float ambulanceConfidence =
                    output[
                        4 *
                        predictionCount +
                        i
                    ];


                float jetbotConfidence =
                    output[
                        5 *
                        predictionCount +
                        i
                    ];


                int classId;

                float confidence;


                // =================================================
                // Class 결정
                //
                // Class 0 = Ambulance
                // Class 1 = JetBot
                // =================================================
                if (
                    ambulanceConfidence >
                    jetbotConfidence
                )
                {
                    classId =
                        0;


                    confidence =
                        ambulanceConfidence;
                }
                else
                {
                    classId =
                        1;


                    confidence =
                        jetbotConfidence;
                }


                if (
                    confidence <
                    confidenceThreshold
                )
                {
                    continue;
                }


                // =================================================
                // Letterbox 좌표
                // ->
                // 원본 Frame 좌표
                // =================================================
                centerX =
                    (centerX -
                     padLeft)
                    /
                    scale;


                centerY =
                    (centerY -
                     padTop)
                    /
                    scale;


                width =
                    width /
                    scale;


                height =
                    height /
                    scale;


                detections.Add(
                    new Detection
                    {
                        X =
                            centerX,

                        Y =
                            centerY,

                        Width =
                            width,

                        Height =
                            height,

                        Confidence =
                            confidence,

                        ClassId =
                            classId
                    }
                );
            }


            // =====================================================
            // NMS
            // =====================================================
            return
                ApplyNms(
                    detections,
                    0.45f
                );
        }


        // =========================================================
        // Class-aware NMS
        // =========================================================
        private List<Detection> ApplyNms(
            List<Detection> detections,
            float iouThreshold)
        {
            List<Detection> result =
                new List<Detection>();


            List<Detection> sortedDetections =
                detections
                    .OrderByDescending(
                        d =>
                            d.Confidence
                    )
                    .ToList();


            while (
                sortedDetections.Count >
                0
            )
            {
                Detection best =
                    sortedDetections[0];


                result.Add(
                    best
                );


                sortedDetections
                    .RemoveAt(
                        0
                    );


                sortedDetections =
                    sortedDetections
                        .Where(
                            d =>
                                d.ClassId !=
                                best.ClassId
                                ||
                                CalculateIoU(
                                    best,
                                    d
                                )
                                <
                                iouThreshold
                        )
                        .ToList();
            }


            return result;
        }


        // =========================================================
        // IoU
        // =========================================================
        private float CalculateIoU(
            Detection a,
            Detection b)
        {
            float aLeft =
                a.X -
                a.Width / 2f;


            float aTop =
                a.Y -
                a.Height / 2f;


            float aRight =
                a.X +
                a.Width / 2f;


            float aBottom =
                a.Y +
                a.Height / 2f;


            float bLeft =
                b.X -
                b.Width / 2f;


            float bTop =
                b.Y -
                b.Height / 2f;


            float bRight =
                b.X +
                b.Width / 2f;


            float bBottom =
                b.Y +
                b.Height / 2f;


            float intersectionLeft =
                Math.Max(
                    aLeft,
                    bLeft
                );


            float intersectionTop =
                Math.Max(
                    aTop,
                    bTop
                );


            float intersectionRight =
                Math.Min(
                    aRight,
                    bRight
                );


            float intersectionBottom =
                Math.Min(
                    aBottom,
                    bBottom
                );


            float intersectionWidth =
                Math.Max(
                    0,
                    intersectionRight -
                    intersectionLeft
                );


            float intersectionHeight =
                Math.Max(
                    0,
                    intersectionBottom -
                    intersectionTop
                );


            float intersectionArea =
                intersectionWidth *
                intersectionHeight;


            float areaA =
                a.Width *
                a.Height;


            float areaB =
                b.Width *
                b.Height;


            float unionArea =
                areaA +
                areaB -
                intersectionArea;


            if (
                unionArea <= 0
            )
            {
                return 0;
            }


            return
                intersectionArea /
                unionArea;
        }


        // =========================================================
        // YOLO 모델 로드
        //
        // OpenVINO CPU 사용
        // =========================================================
        public string LoadModel()
        {
            try
            {
                string modelPath =
                    Path.Combine(
                        AppDomain
                            .CurrentDomain
                            .BaseDirectory,

                        "AIModels",

                        "best.onnx"
                    );


                if (
                    !File.Exists(
                        modelPath
                    )
                )
                {
                    return
                        "MODEL FILE NOT FOUND";
                }


                // =================================================
                // OpenVINO Session
                // =================================================
                SessionOptions options =
                    new SessionOptions();


                options.GraphOptimizationLevel =
                    GraphOptimizationLevel
                        .ORT_ENABLE_ALL;


                // OpenVINO CPU
                options
                    .AppendExecutionProvider_OpenVINO(
                        "CPU"
                    );


                _session =
                    new InferenceSession(
                        modelPath,
                        options
                    );


                InputName =
                    _session
                        .InputMetadata
                        .Keys
                        .FirstOrDefault();


                OutputName =
                    _session
                        .OutputMetadata
                        .Keys
                        .FirstOrDefault();


                return
                    "MODEL LOADED - OPENVINO CPU";
            }
            catch (
                Exception ex
            )
            {
                return
                    "MODEL LOAD ERROR: " +
                    ex.Message;
            }
        }


        // =========================================================
        // 모델 정보
        // =========================================================
        public string GetModelInfo()
        {
            if (
                _session == null
            )
            {
                return
                    "MODEL NOT LOADED";
            }


            string result =
                "";


            result +=
                "=== INPUTS ===" +
                Environment.NewLine;


            foreach (
                var input
                in _session.InputMetadata
            )
            {
                result +=
                    input.Key +
                    " [" +
                    string.Join(
                        ", ",
                        input.Value.Dimensions
                    ) +
                    "]" +
                    Environment.NewLine;
            }


            result +=
                Environment.NewLine;


            result +=
                "=== OUTPUTS ===" +
                Environment.NewLine;


            foreach (
                var output
                in _session.OutputMetadata
            )
            {
                result +=
                    output.Key +
                    " [" +
                    string.Join(
                        ", ",
                        output.Value.Dimensions
                    ) +
                    "]" +
                    Environment.NewLine;
            }


            result +=
                Environment.NewLine;


            result +=
                "=== MODEL METADATA ===" +
                Environment.NewLine;


            foreach (
                var item
                in _session
                    .ModelMetadata
                    .CustomMetadataMap
            )
            {
                result +=
                    item.Key +
                    " = " +
                    item.Value +
                    Environment.NewLine;
            }


            return result;
        }


        // =========================================================
        // Dispose
        // =========================================================
        public void Dispose()
        {
            _session?.Dispose();


            _session =
                null;
        }
    }
}