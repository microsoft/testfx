// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using TestingPlatformExplorer.TestingFramework;


namespace TestingPlatformExplorer;

public class DeviceTests
{

    [TestMethod()]
    public async Task ButtonRippleEffect()
    {
        var layout = new Grid();

        var button = new Button
        {
            Text = "Text",
          //  Background = new LinearGradient(Colors.Red, Colors.Orange),
        };

        layout.Add(button);

        var clicked = false;

        button.Clicked += delegate
        {
            clicked = true;
        };

       // await PerformClick(button);

        Assert.Equals(clicked, true);

        //await AttachAndRun(button, async (handler) =>
        //{
        //    await Task.Delay(100);

        //    var hasRipple = GetNativeHasRippleDrawable(handler);
        //    Assert.True(hasRipple);
        //});
    }

    //public interface IStubBase : IView, IVisualTreeElement, IToolTipElement, IPropertyMapperView
    //{
    //    new string AutomationId { get; set; }

    //    new double Width { get; set; }

    //    new double Height { get; set; }

    //    new double MaximumWidth { get; set; }

    //    new double MaximumHeight { get; set; }

    //    new double MinimumWidth { get; set; }

    //    new double MinimumHeight { get; set; }

    //    new double TranslationX { get; set; }

    //    new double TranslationY { get; set; }

    //    new double Scale { get; set; }

    //    new double ScaleX { get; set; }

    //    new double ScaleY { get; set; }

    //    new double Rotation { get; set; }

    //    new double RotationX { get; set; }

    //    new double RotationY { get; set; }

    //    new double AnchorX { get; set; }

    //    new double AnchorY { get; set; }

    //    new Thickness Margin { get; set; }

    //    new FlowDirection FlowDirection { get; set; }

    //    new double Opacity { get; set; }

    //    new Visibility Visibility { get; set; }

    //    new Semantics Semantics { get; set; }

    //    new Paint Background { get; set; }

    //    new IShape Clip { get; set; }

    //    new bool InputTransparent { get; set; }
    //    new IElement Parent { get; set; }

    //    PropertyMapper PropertyMapperOverrides { get; set; }
    //}

    //public class ElementStub : IElement
    //{
    //    public IElement Parent { get; set; }

    //    public IElementHandler Handler { get; set; }
    //}

    //public class StubBase : ElementStub, IStubBase
    //{
    //    IElementHandler IElement.Handler
    //    {
    //        get => Handler;
    //        set => Handler = (IViewHandler)value;
    //    }

    //    public bool IsEnabled { get; set; } = true;

    //    public bool IsFocused { get; set; }

    //    public IList<IView> Children { get; set; } = new List<IView>();

    //    public Visibility Visibility { get; set; } = Visibility.Visible;

    //    public double Opacity { get; set; } = 1.0d;

    //    public Paint Background { get; set; }

    //    public Rect Frame { get; set; }

    //    public new IViewHandler Handler
    //    {
    //        get => (IViewHandler)base.Handler;
    //        set => base.Handler = value;
    //    }

    //    public IShape Clip { get; set; }

    //    public IShadow Shadow { get; set; }

    //    public Size DesiredSize { get; set; } = new Size(50, 50);

    //    public double Width { get; set; } = 50;

    //    public double Height { get; set; } = 50;

    //    public double MaximumWidth { get; set; } =  int.MaxValue;

    //    public double MaximumHeight { get; set; } = int.MaxValue;

    //    public double MinimumWidth { get; set; } = 0;

    //    public double MinimumHeight { get; set; } = 0;

    //    public double TranslationX { get; set; }

    //    public double TranslationY { get; set; }

    //    public double Scale { get; set; } = 1d;

    //    public double ScaleX { get; set; } = 1d;

    //    public double ScaleY { get; set; } = 1d;

    //    public double Rotation { get; set; }

    //    public double RotationX { get; set; }

    //    public double RotationY { get; set; }

    //    public double AnchorX { get; set; } = .5d;

    //    public double AnchorY { get; set; } = .5d;

    //    public Thickness Margin { get; set; }

    //    public string AutomationId { get; set; }

    //    public FlowDirection FlowDirection { get; set; } = FlowDirection.LeftToRight;

    //    public LayoutAlignment HorizontalLayoutAlignment { get; set; }

    //    public LayoutAlignment VerticalLayoutAlignment { get; set; }

    //    public Semantics Semantics { get; set; } = new Semantics();

    //    public int ZIndex { get; set; }

    //    public bool InputTransparent { get; set; }

    //    public ToolTip ToolTip { get; set; }

    //    public Size Arrange(Rect bounds)
    //    {
    //        Frame = bounds;
    //        DesiredSize = bounds.Size;

    //        // If this view is attached to the visual tree then let's arrange it
    //        if (IsLoaded)
    //            Handler?.PlatformArrange(Frame);

    //        return DesiredSize;
    //    }

    //    protected bool SetProperty<T>(ref T backingStore, T value,
    //        [CallerMemberName] string propertyName = "",
    //        Action<T, T> onChanged = null)
    //    {
    //        if (EqualityComparer<T>.Default.Equals(backingStore, value))
    //            return false;

    //        var oldValue = backingStore;
    //        backingStore = value;
    //        Handler?.UpdateValue(propertyName);
    //        onChanged?.Invoke(oldValue, value);
    //        return true;
    //    }

    //    public void InvalidateArrange()
    //    {
    //    }

    //    public void InvalidateMeasure()
    //    {
    //    }

    //    public bool Focus()
    //    {
    //        return true;
    //        //FocusRequest focusRequest = new FocusRequest();
    //        //return Handler?.InvokeWithResult(nameof(IView.Focus), focusRequest) ?? false;
    //    }

    //    public void Unfocus()
    //    {
    //        IsFocused = false;
    //    }

    //    public Size Measure(double widthConstraint, double heightConstraint)
    //    {
    //        if (Handler != null)
    //        {
    //            DesiredSize = Handler.GetDesiredSize(widthConstraint, heightConstraint);
    //            return DesiredSize;
    //        }

    //        return new Size(widthConstraint, heightConstraint);
    //    }

    //    IReadOnlyList<IVisualTreeElement> IVisualTreeElement.GetVisualChildren() => this.Children.Cast<IVisualTreeElement>().ToList().AsReadOnly();

    //    IVisualTreeElement IVisualTreeElement.GetVisualParent() => this.Parent as IVisualTreeElement;

    //    PropertyMapper IPropertyMapperView.GetPropertyMapperOverrides() =>
    //        PropertyMapperOverrides;

    //    public PropertyMapper PropertyMapperOverrides
    //    {
    //        get;
    //        set;
    //    }

    //    public bool IsLoaded
    //    {
    //        get
    //        {
    //            return true;
    //         //   return (Handler as IPlatformViewHandler)?.PlatformView?.IsLoaded() == true;
    //        }
    //    }
    //}

    //public class LayoutStub : StubBase, Microsoft.Maui.ILayout
    //{
    //    ILayoutManager _layoutManager;

    //    public ILayoutHandler LayoutHandler => Handler as ILayoutHandler;

    //    public void Add(IView child)
    //    {
    //        Children.Add(child);
    //    }

    //    public bool Remove(IView child)
    //    {
    //        return Children.Remove(child);
    //    }

    //    public int IndexOf(IView item)
    //    {
    //        return Children.IndexOf(item);
    //    }

    //    public void Insert(int index, IView item)
    //    {
    //        Children.Insert(index, item);
    //    }

    //    public void RemoveAt(int index)
    //    {
    //        Children.RemoveAt(index);
    //    }

    //    public void Clear()
    //    {
    //        Children.Clear();
    //    }

    //    public bool Contains(IView item)
    //    {
    //        return Children.Contains(item);
    //    }

    //    public void CopyTo(IView[] array, int arrayIndex)
    //    {
    //        Children.CopyTo(array, arrayIndex);
    //    }

    //    public IEnumerator<IView> GetEnumerator()
    //    {
    //        return Children.GetEnumerator();
    //    }

    //    IEnumerator IEnumerable.GetEnumerator()
    //    {
    //        return Children.GetEnumerator();
    //    }

    //    public Size CrossPlatformMeasure(double widthConstraint, double heightConstraint)
    //    {
    //        return LayoutManager.Measure(widthConstraint, heightConstraint);
    //    }

    //    public Size CrossPlatformArrange(Rect bounds)
    //    {
    //        return LayoutManager.ArrangeChildren(bounds);
    //    }

    //    public Thickness Padding { get; set; }
    //    public int Count => Children.Count;
    //    public bool IsReadOnly => Children.IsReadOnly;

    //    ILayoutManager LayoutManager => _layoutManager ??= CreateLayoutManager();

    //    protected virtual ILayoutManager CreateLayoutManager() => new LayoutManagerStub();

    //    public bool IgnoreSafeArea => false;

    //    public bool ClipsToBounds { get; set; }

    //    public IView this[int index] { get => Children[index]; set => Children[index] = value; }
    //}

    //public class LayoutManagerStub : ILayoutManager
    //{
    //    public Size ArrangeChildren(Rect bounds)
    //    {
    //        return bounds.Size;
    //    }

    //    public Size Measure(double widthConstraint, double heightConstraint)
    //    {
    //        return new Size(widthConstraint - 1, heightConstraint - 1);
    //    }
    //}
}
