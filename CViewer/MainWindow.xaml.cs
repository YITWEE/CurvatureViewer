using g3;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;

namespace CViewer
{


    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        WaitingWindow WaitingDoneWindow;
        DMesh3 OriginalMeshes;
        MainViewModel ViewModel = new MainViewModel();

        private Color TopColor = Brushes.DeepPink.Color;
        private Color MiddleColor = Brushes.Gold.Color;
        private Color BottomColor = Brushes.Lime.Color;

        double MaxValue = double.MaxValue;
        double MinValue = double.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            Hv3dMain.DataContext = ViewModel;
        }

        private void BtnInput_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "请要计算曲率的文件";
            dialog.Filter = "stl模型|*.stl";
            if (dialog.ShowDialog() == true)
            {
                WaitingDoneWindow = new WaitingWindow("正在导入STL模型，请等待");
                WaitingDoneWindow.Owner = this;
                DMesh3Builder builder = new DMesh3Builder();
                Task.Run(() =>
                {
                    StandardMeshReader reader = new StandardMeshReader() { MeshBuilder = builder };
                    return reader.Read(dialog.FileName, ReadOptions.Defaults);
                }).ContinueWith((result) =>
                {
                    if (result.IsCompleted)
                    {
                        if (result.Result.code == IOCode.Ok)
                        {
                            OriginalMeshes = builder.Meshes.First();
                        }
                        MeshNormals.QuickCompute(OriginalMeshes);
                        Vector3Collection points = new Vector3Collection(OriginalMeshes.Vertices().ToList().ConvertAll(
                            v => new Vector3(Convert.ToSingle(v.x), Convert.ToSingle(v.y), Convert.ToSingle(v.z))));
                        IntCollection triangleIndices = new IntCollection();
                        Color4Collection colors = new Color4Collection();
                        Vector3Collection normals = new Vector3Collection(OriginalMeshes.NormalsBuffer.Count() / 3);
                        int NormalsCount = OriginalMeshes.NormalsBuffer.Count() / 3;
                        for (int i = 0; i < NormalsCount; i++)
                        {
                            normals.Add(new Vector3(OriginalMeshes.NormalsBuffer[i * 3], OriginalMeshes.NormalsBuffer[i * 3 + 1], OriginalMeshes.NormalsBuffer[i * 3 + 2]));
                        }

                        foreach (Index3i index3 in OriginalMeshes.Triangles())
                        {
                            triangleIndices.Add(index3.a);
                            triangleIndices.Add(index3.b);
                            triangleIndices.Add(index3.c);
                        }

                        MeshGeometry3D geometry = new MeshGeometry3D();
                        geometry.Positions = points;
                        geometry.Normals = normals;
                        geometry.TriangleIndices = triangleIndices;

                        ViewModel.Geometry.ClearAllGeometryData();
                        ViewModel.Geometry = geometry;
                        ViewModel.Material = PhongMaterials.MediumGray;
                        ResetCamera(geometry.Bound);

                        WaitingDoneWindow.Close();
                    }
                    else if (result.IsFaulted && result.Exception != null)
                    {
                        MessageBox.Show(result.Exception.Message);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
                WaitingDoneWindow.ShowDialog();
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            MaxValue = double.MaxValue;
            MinValue = double.MinValue;
            UpdateView();
        }

        private void UpdateView()
        {
            switch (CmbCuvType.SelectedIndex)
            {
                case 0:
                    DrawGaussianCurvature();
                    break;
                case 1:
                    break;
                case 2:
                    break;
                default:
                    break;
            }
        }

        void DrawGaussianCurvature()
        {
            if (OriginalMeshes == null)
            {
                return;
            }
            List<double> Curvatures = new List<double>(OriginalMeshes.VertexCount);
            var Vertices = OriginalMeshes.Vertices();
            for (int i = 0; i < OriginalMeshes.VertexCount; i++)
            {
                List<int> Tris = new List<int>();
                if (OriginalMeshes.GetVtxTriangles(i, Tris, false) == MeshResult.Ok)
                {

                    double SumAngle = 0;
                    double SumArea = 0;
                    Vector3d center = OriginalMeshes.GetVertex(i);
                    foreach (var tri in Tris)
                    {
                        Vector3d v1, v2;
                        if (OriginalMeshes.GetTriVertex(tri, 0) == center)
                        {
                            v1 = OriginalMeshes.GetTriVertex(tri, 1) - center;
                            v2 = OriginalMeshes.GetTriVertex(tri, 2) - center;
                        }
                        else if (OriginalMeshes.GetTriVertex(tri, 1) == center)
                        {
                            v1 = OriginalMeshes.GetTriVertex(tri, 0) - center;
                            v2 = OriginalMeshes.GetTriVertex(tri, 2) - center;
                        }
                        else
                        {
                            v1 = OriginalMeshes.GetTriVertex(tri, 0) - center;
                            v2 = OriginalMeshes.GetTriVertex(tri, 1) - center;
                        }

                        SumAngle += Vector3d.AngleR(v1.Normalized, v2.Normalized);
                        SumArea += OriginalMeshes.GetTriArea(tri);
                    }
                    Curvatures.Add((2 * Math.PI - SumAngle) / (SumArea / 3));
                    //Curvatures.Add((2 * Math.PI - SumAngle) / (2 * Math.PI));

                    //参考公式：
                    //https://www.cnblogs.com/tongj1981/archive/2008/05/16/1200231.html
                    //https://computergraphics.stackexchange.com/questions/1718/what-is-the-simplest-way-to-compute-principal-curvature-for-a-mesh-triangle
                }
                else
                {
                    Curvatures.Add(0);
                }
            }

            //设置展示范围
            Curvatures = Curvatures.ConvertAll(c =>
            {
                if (c <= MinValue)
                {
                    return MinValue;
                }
                else if (c >= MaxValue)
                {
                    return MaxValue;
                }
                else
                {
                    return c;
                }
            });

            //曲率值归一化[0,1]
            double CurMin = Curvatures.Min();
            TbxMin.Text = CurMin.ToString("F3");
            double CurMax = Curvatures.Max();
            TbxMax.Text = CurMax.ToString("F3");
            //double AbsMax = Math.Max(Math.Abs(CurMin), Math.Abs(CurMax));
            double CurRange = CurMax - CurMin;
            TbkMiddle.Text = (CurMin + CurRange / 2).ToString("F3");
            Curvatures = Curvatures.ConvertAll(c => (c - CurMin) / CurRange);

            //TODO:直方图均值化

            Vector3Collection points = new Vector3Collection(OriginalMeshes.Vertices().ToList().ConvertAll(
                v => new Vector3(Convert.ToSingle(v.x), Convert.ToSingle(v.y), Convert.ToSingle(v.z))));
            IntCollection triangles = new IntCollection();
            Vector3Collection normals = new Vector3Collection(OriginalMeshes.NormalsBuffer.Count() / 3);
            int NormalsCount = OriginalMeshes.NormalsBuffer.Count() / 3;
            for (int i = 0; i < NormalsCount; i++)
            {
                normals.Add(new Vector3(OriginalMeshes.NormalsBuffer[i * 3], OriginalMeshes.NormalsBuffer[i * 3 + 1], OriginalMeshes.NormalsBuffer[i * 3 + 2]));
            }

            List<Color4> colors = new List<Color4>();
            foreach (Index3i index3 in OriginalMeshes.Triangles())
            {
                triangles.Add(index3.a);
                triangles.Add(index3.b);
                triangles.Add(index3.c);
            }

            colors = Curvatures.ConvertAll(c => {
                if (c<0.5)
                {
                    float r = Convert.ToSingle(((MiddleColor.R - BottomColor.R) / 0.5 * c + BottomColor.R)) / 255;
                    float g = Convert.ToSingle(((MiddleColor.G - BottomColor.G) / 0.5 * c + BottomColor.G)) / 255;
                    float b = Convert.ToSingle(((MiddleColor.B - BottomColor.B) / 0.5 * c + BottomColor.B)) / 255;
                    return new Color4(r, g, b, 1);
                }
                else
                {
                    float r = Convert.ToSingle(((TopColor.R - MiddleColor.R) / 0.5 * (c - 0.5) + MiddleColor.R)) / 255;
                    float g = Convert.ToSingle(((TopColor.G - MiddleColor.G) / 0.5 * (c - 0.5) + MiddleColor.G)) / 255;
                    float b = Convert.ToSingle(((TopColor.B - MiddleColor.B) / 0.5 * (c - 0.5) + MiddleColor.B)) / 255;
                    return new Color4(r, g, b, 1);
                }
            });

            MeshGeometry3D geometry = new MeshGeometry3D();
            geometry.Positions = points;
            geometry.Normals = normals;
            geometry.TriangleIndices = triangles;
            geometry.Colors = new Color4Collection(colors);

            ViewModel.Geometry.ClearAllGeometryData();
            ViewModel.Geometry = geometry;
            ViewModel.Material = new VertColorMaterial();
            ResetCamera(geometry.Bound);

        }

        void ResetCamera(BoundingBox bound)
        {
            float maxWidth = bound.Size.Length() + 20;
            float offset = Convert.ToSingle(maxWidth / 2 / 0.414 * 0.707);
            Vector3 cameraCenter = bound.Center + new Vector3(-offset, -offset, offset);

            PerspectiveCamera camera = new PerspectiveCamera();
            camera.Position = new System.Windows.Media.Media3D.Point3D(cameraCenter.X, cameraCenter.Y, cameraCenter.Z);
            camera.LookDirection = new System.Windows.Media.Media3D.Vector3D(1, 1, -1);
            camera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
            camera.FieldOfView = 45;
            camera.NearPlaneDistance = 0.001;
            camera.FarPlaneDistance = double.PositiveInfinity;
            ViewModel.Camera = camera;
        }

        private void SpkTop_ColorChanged(object sender, RoutedEventArgs e)
        {
            TopColor = SpkTop.SelectedColor;
        }

        private void SpkMiddle_ColorChanged(object sender, RoutedEventArgs e)
        {
            MiddleColor = SpkMiddle.SelectedColor;
        }

        private void SpkBottom_ColorChanged(object sender, RoutedEventArgs e)
        {
            BottomColor = SpkBottom.SelectedColor;
        }

        private void Hv3dMain_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                ResetCamera(ViewModel.Geometry.Bound);
            }
        }

        private void TbxMax_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TbxMax_LostFocus(null, null);
            }
        }

        private void TbxMax_LostFocus(object sender, RoutedEventArgs e)
        {
            double.TryParse(TbxMax.Text, out double input);
            double.TryParse(TbxMin.Text, out double min);
            if (input > min)
            {
                MaxValue = input;
                UpdateView();
            }
        }

        private void TbxMin_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TbxMin_LostFocus(null, null);
            }
        }

        private void TbxMin_LostFocus(object sender, RoutedEventArgs e)
        {
            double.TryParse(TbxMin.Text, out double input);
            double.TryParse(TbxMax.Text, out double max);
            if (input < max)
            {
                MinValue = input;
                UpdateView();
            }
        }
        //List<double> EqualizeHist(List<double> values, int grade)
        //{
        //    int[] mp = new int[grade];
        //    foreach (var value in values)
        //    {
        //        mp[Convert.ToInt32(Math.Floor(value * grade))]++;
        //    }
        //    double[] graypro = new double[grade];
        //    for (int i = 0; i < grade; i++)
        //    {
        //        graypro[i] = mp[i] / (values.Count);

        //    }
        //    double[] graysumpro = new double[grade];
        //    graysumpro[0] = graypro[0];
        //    for (int i = 1; i < grade; i++)
        //    {
        //        graysumpro[i] = graysumpro[i - 1] + graypro[i];

        //    }
        //    byte[] grayequ = new byte[grade];
        //    for (int i = 0; i < grade; i++)
        //    {
        //        grayequ[i] = (byte)(graysumpro[i] * grade + 0.5);

        //    }
        //    //根据累计频率进行转换
        //    List<double> temp = new List<double>(values.Count);
        //    foreach (var value in values)
        //    {
        //        temp.Add(grayequ[value]];);
        //    }
        //    return temp;
        //}
    }
}
