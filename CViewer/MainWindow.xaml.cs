using g3;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
        DMesh3 OriginalMesh;
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
                            OriginalMesh = builder.Meshes.First();
                        }
                        MeshNormals.QuickCompute(OriginalMesh);
                        Vector3Collection points = new Vector3Collection(OriginalMesh.TriangleCount * 3);                        
                        Vector3Collection normals = new Vector3Collection(OriginalMesh.TriangleCount * 3);

                        var a = new Vector3d();
                        var b = new Vector3d();
                        var c = new Vector3d();
                        var n = new Vector3d();
                        foreach (int index in OriginalMesh.TriangleIndices())
                        {
                            OriginalMesh.GetTriVertices(index, ref a, ref b, ref c);
                            points.Add(new Vector3(Convert.ToSingle(a.x), Convert.ToSingle(a.y), Convert.ToSingle(a.z)));
                            points.Add(new Vector3(Convert.ToSingle(b.x), Convert.ToSingle(b.y), Convert.ToSingle(b.z)));
                            points.Add(new Vector3(Convert.ToSingle(c.x), Convert.ToSingle(c.y), Convert.ToSingle(c.z)));
                            n = OriginalMesh.GetTriNormal(index);
                            normals.Add(new Vector3(Convert.ToSingle(n.x), Convert.ToSingle(n.y), Convert.ToSingle(n.z)));
                            normals.Add(new Vector3(Convert.ToSingle(n.x), Convert.ToSingle(n.y), Convert.ToSingle(n.z)));
                            normals.Add(new Vector3(Convert.ToSingle(n.x), Convert.ToSingle(n.y), Convert.ToSingle(n.z)));
                        }

                        MeshGeometry3D geometry = new MeshGeometry3D();
                        geometry.Positions = points;
                        geometry.Normals = normals;
                        geometry.TriangleIndices = new IntCollection(Enumerable.Range(0, points.Count()));

                        ViewModel.Geometry.ClearAllGeometryData();
                        ViewModel.Geometry = geometry;
                        ViewModel.Material = PhongMaterials.MediumGray;
                        ResetCamera(geometry.Bound);
                        TbkTitle.Text = "";
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
            if (OriginalMesh == null)
            {
                return;
            }

            WaitingDoneWindow = new WaitingWindow("正在计算曲率，请等待");
            WaitingDoneWindow.Owner = this;
            TbkTitle.Text = CmbCuvType.Text;
            int SelectedIndex = CmbCuvType.SelectedIndex;
            Task.Run(() =>
            {
                switch (SelectedIndex)
                {
                    case 0:
                        return GetGaussianCurvature(OriginalMesh);
                    case 1:
                        return GetMeanCurvature(OriginalMesh);
                    case 2:
                        return GetMaxPrincipalCurvature(OriginalMesh);
                    case 3:
                        return GetMinPrincipalCurvature(OriginalMesh);
                    default:
                        return new List<double>();
                }
            }).ContinueWith((result) =>
            {
                if (result.IsCompleted)
                {
                    DrawCurvature(result.Result);
                    WaitingDoneWindow.Close();
                }
                else if (result.IsFaulted && result.Exception != null)
                {
                    MessageBox.Show(result.Exception.Message);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
            WaitingDoneWindow.ShowDialog();
        }

        List<double> GetGaussianCurvature(DMesh3 mesh)
        {
            if (mesh == null)
            {
                return null;
            }
            List<double> Curvatures = new List<double>(mesh.VertexCount);
            foreach (var i in mesh.VertexIndices())
            {
                if (mesh.IsBoundaryVertex(i) == false)
                {
                    List<int> Tris = new List<int>();
                    if (mesh.GetVtxTriangles(i, Tris, false) == MeshResult.Ok)
                    {

                        double SumAngle = 0;
                        double SumArea = 0;
                        Vector3d center = mesh.GetVertex(i);
                        foreach (var tri in Tris)
                        {
                            Vector3d v1, v2;
                            if (mesh.GetTriVertex(tri, 0) == center)
                            {
                                v1 = mesh.GetTriVertex(tri, 1) - center;
                                v2 = mesh.GetTriVertex(tri, 2) - center;
                            }
                            else if (mesh.GetTriVertex(tri, 1) == center)
                            {
                                v1 = mesh.GetTriVertex(tri, 0) - center;
                                v2 = mesh.GetTriVertex(tri, 2) - center;
                            }
                            else
                            {
                                v1 = mesh.GetTriVertex(tri, 0) - center;
                                v2 = mesh.GetTriVertex(tri, 1) - center;
                            }

                            SumAngle += Vector3d.AngleR(v1.Normalized, v2.Normalized);
                            SumArea += mesh.GetTriArea(tri);
                        }

                        Curvatures.Add((2 * Math.PI - SumAngle) / (SumArea / 3));

                        //参考公式：
                        //https://www.cnblogs.com/tongj1981/archive/2008/05/16/1200231.html
                        //https://computergraphics.stackexchange.com/questions/1718/what-is-the-simplest-way-to-compute-principal-curvature-for-a-mesh-triangle
                    }
                    else
                    {
                        Curvatures.Add(0);
                    }
                }
                else
                {
                    Curvatures.Add(0);
                }
            }
            return Curvatures;
        }

        List<double> GetMeanCurvature(DMesh3 mesh)
        {
            if (mesh == null)
            {
                return null;
            }
            List<double> Curvatures = new List<double>(mesh.VertexCount);
            foreach (var i in mesh.VertexIndices())
            {
                if (mesh.IsBoundaryVertex(i) == false)
                {
                    Vector3d SumVector = Vector3d.Zero;
                    double SumArea = 0;

                    Vector3d VectorI = mesh.GetVertex(i);
                    var Edges = mesh.VtxEdgesItr(i);
                    foreach (var edge in Edges)
                    {
                        int j = mesh.edge_other_v(edge, i);
                        Vector3d VectorJ = mesh.GetVertex(j);

                        var tris = mesh.GetEdgeT(edge);

                        var triAvs = mesh.GetTriangle(tris.a).array.ToList();
                        triAvs.Remove(i);
                        triAvs.Remove(j);
                        Vector3d VectorA = mesh.GetVertex(triAvs[0]);

                        var triBvs = mesh.GetTriangle(tris.b).array.ToList();
                        triBvs.Remove(i);
                        triBvs.Remove(j);
                        Vector3d VectorB = mesh.GetVertex(triBvs[0]);

                        double cotA = 1 / Math.Tan(Vector3d.AngleR(VectorI - VectorA, VectorJ - VectorA));
                        double cotB = 1 / Math.Tan(Vector3d.AngleR(VectorI - VectorB, VectorJ - VectorB));

                        if (double.IsInfinity(cotA) || double.IsNaN(cotA) || double.IsInfinity(cotB) || double.IsNaN(cotB))
                        {
                            continue;
                        }

                        SumVector += (cotA + cotB) * (VectorJ - VectorI);
                        SumArea += mesh.GetTriArea(tris.a);
                    }
                    if (SumArea == 0)
                    {
                        Curvatures.Add(double.PositiveInfinity);
                    }
                    else
                    {
                        Curvatures.Add((SumVector / (2 * (SumArea / 3))).Length / 2);
                    }
                }
                else
                {
                    Curvatures.Add(0);
                }

                //参考公式：
                //https://blog.csdn.net/chenbb1989/article/details/124363979
                //http://rodolphe-vaillant.fr/entry/33/curvature-of-a-triangle-mesh-definition-and-computation
            }
            return Curvatures;
        }

        List<double> GetMaxPrincipalCurvature(DMesh3 mesh)
        {
            var gc = GetGaussianCurvature(mesh);
            var mc = GetMeanCurvature(mesh);
            if (gc == null || mc == null)
            {
                return new List<double>();
            }
            if (gc.Count != mc.Count)
            {
                return new List<double>();
            }
            List<double> result = new List<double>(gc.Count);
            for (int i = 0; i < gc.Count; i++)
            {
                var temp = mc[i] * mc[i] - gc[i];
                result.Add(mc[i] + Math.Sqrt(temp < 0 ? 0 : temp));
            }
            return result;
        }

        List<double> GetMinPrincipalCurvature(DMesh3 mesh)
        {
            var gc = GetGaussianCurvature(mesh);
            var mc = GetMeanCurvature(mesh);
            if (gc == null || mc == null)
            {
                return new List<double>();
            }
            if (gc.Count != mc.Count)
            {
                return new List<double>();
            }
            List<double> result = new List<double>(gc.Count);
            for (int i = 0; i < gc.Count; i++)
            {
                var temp = mc[i] * mc[i] - gc[i];
                result.Add(mc[i] - Math.Sqrt(temp < 0 ? 0 : temp));
            }
            return result;
        }

        void DrawCurvature( List<double> Curvatures)
        {
            if (Curvatures == null)
            {
                return;
            }
            if (Curvatures.Count == 0)
            {
                return;
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
            double CurMin;
            if (MinValue == double.MinValue)
            {
                CurMin = Curvatures.Min();
            }
            else
            {
                CurMin = MinValue;
            }
            TbxMin.Text = CurMin.ToString("F3");
            double CurMax;
            if (MaxValue == double.MaxValue)
            {
                CurMax = Curvatures.Max();
            }
            else
            {
                CurMax = MaxValue;
            }
            TbxMax.Text = CurMax.ToString("F3");
            double CurRange = CurMax - CurMin;
            TbkMiddle.Text = (CurMin + CurRange / 2).ToString("F3");
            Curvatures = Curvatures.ConvertAll(c => (c - CurMin) / CurRange);

            Vector3Collection points = new Vector3Collection(OriginalMesh.Vertices().ToList().ConvertAll(
                v => new Vector3(Convert.ToSingle(v.x), Convert.ToSingle(v.y), Convert.ToSingle(v.z))));
            IntCollection triangles = new IntCollection();
            Vector3Collection normals = new Vector3Collection(OriginalMesh.NormalsBuffer.Count() / 3);
            int NormalsCount = OriginalMesh.NormalsBuffer.Count() / 3;
            for (int i = 0; i < NormalsCount; i++)
            {
                normals.Add(new Vector3(OriginalMesh.NormalsBuffer[i * 3], OriginalMesh.NormalsBuffer[i * 3 + 1], OriginalMesh.NormalsBuffer[i * 3 + 2]));
            }

            List<Color4> colors = new List<Color4>();
            foreach (Index3i index3 in OriginalMesh.Triangles())
            {
                triangles.Add(index3.a);
                triangles.Add(index3.b);
                triangles.Add(index3.c);
            }

            colors = Curvatures.ConvertAll(c => {
                if (c < 0.5)
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
            //ResetCamera(geometry.Bound);
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
                double.TryParse(TbxMax.Text, out double input);
                double.TryParse(TbxMin.Text, out double min);
                if (input > min)
                {
                    MaxValue = input;
                    MaxTemp = input;
                    UpdateView();
                }
                else
                {
                    TbxMax.Text = MaxTemp.ToString();
                }
            }
        }

        double MaxTemp = 0;
        private void TbxMax_GotFocus(object sender, RoutedEventArgs e)
        {
            double.TryParse(TbxMax.Text, out MaxTemp);
        }

        private void TbxMax_LostFocus(object sender, RoutedEventArgs e)
        {
            TbxMax.Text = MaxTemp.ToString();
        }

        private void TbxMin_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                double.TryParse(TbxMin.Text, out double input);
                double.TryParse(TbxMax.Text, out double max);
                if (input < max)
                {
                    MinValue = input;
                    UpdateView();
                }
                else
                {
                    TbxMin.Text = MinTemp.ToString();
                }
            }
        }

        double MinTemp = 0;
        private void TbxMin_GotFocus(object sender, RoutedEventArgs e)
        {
            double.TryParse(TbxMin.Text, out MinTemp);
        }

        private void TbxMin_LostFocus(object sender, RoutedEventArgs e)
        {
            TbxMin.Text = MinTemp.ToString();
        }

        private void CmbCuvType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MaxValue = double.MaxValue;
            MinValue = double.MinValue;
            UpdateView();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title += " " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        }
    }
}
