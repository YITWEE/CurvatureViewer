using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CViewer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private EffectsManager effectsManager;
        private Camera camera;
        private Geometry3D geometry = new MeshGeometry3D();
        private Material material;
        public EffectsManager EffectsManager { get => effectsManager; set => Set(ref effectsManager, value); }
        public Camera Camera { get => camera; set => Set(ref camera, value); }
        public Geometry3D Geometry { get => geometry; set => Set(ref geometry, value); }
        public Material Material { get => material; set => Set(ref material, value); }

        public MainViewModel()
        {
            EffectsManager = new DefaultEffectsManager();
            Camera = new OrthographicCamera();
            //var build = new MeshBuilder();
            //build.AddCube();
            //Geometry = build.ToMesh();
            Material = PhongMaterials.MediumGray; ;
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string info = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        protected bool Set<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
        {
            if (object.Equals(backingField, value))
            {
                return false;
            }

            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}
