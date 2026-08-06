using System;
using Autofac;
using Mate.MVVM.Core;
using Mate.MVVM.ViewModels;
using Mate.MVVM.Views;
using Mate.Services.Implementations;
using Mate.Services.Interfaces;

namespace Mate.Services.DI;

public static class AutofacConfig
{
    public static IContainer GetConfiguredContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<NavigationService>().As<INavigationService>().SingleInstance();
        builder.RegisterType<TrayService>().As<ITrayService>().SingleInstance();
        builder.RegisterType<WindowsMediaSessionService>().As<IMediaSessionService>().SingleInstance();
        builder.RegisterType<FileShelfService>().As<IFileShelfService>().SingleInstance().AutoActivate();
        builder.RegisterType<WindowsClipboardHistoryService>().As<IClipboardHistoryService>().SingleInstance();
        builder.RegisterType<SnippetStorageService>().As<ISnippetStorageService>().SingleInstance();
        builder.RegisterType<PrivateBrowserService>().As<IPrivateBrowserService>().SingleInstance();
        builder.RegisterType<ThemeService>().As<IThemeService>().SingleInstance();
        builder.RegisterType<WindowsAutoStartService>().As<IAutoStartService>().SingleInstance();

        builder.Register<Func<Type, BaseViewModel>>(context =>
        {
            var componentContext = context.Resolve<IComponentContext>();
            return viewModelType => (BaseViewModel)componentContext.Resolve(viewModelType);
        }).SingleInstance();

        builder.RegisterType<MainWindowViewModel>().SingleInstance();
        builder.RegisterType<MusicViewModel>().SingleInstance();
        builder.RegisterType<FolderViewModel>().SingleInstance();
        builder.RegisterType<ClipboardViewModel>().SingleInstance();
        builder.RegisterType<SnippetsViewModel>().SingleInstance();
        builder.RegisterType<IncognitoViewModel>().SingleInstance();
        builder.RegisterType<TranslatorViewModel>().SingleInstance();

        builder.Register(context => new MainWindow
        {
            DataContext = context.Resolve<MainWindowViewModel>()
        }).SingleInstance();

        return builder.Build();
    }
}
