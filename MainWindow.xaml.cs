using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PortProcessManager.ViewModels;
using PortProcessManager.Services;

namespace PortProcessManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // 挂载右键菜单打开事件，用于同步多选内容
        if (MainGrid.ContextMenu != null)
        {
            MainGrid.ContextMenu.Opened += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.SelectedItemsSnapshot = MainGrid.SelectedItems.Cast<PortRow>().ToList();
                }
            };
        }
    }
}
