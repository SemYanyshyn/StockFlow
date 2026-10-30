# Інструкція: як додати новий екран у StockFlow

Цей документ описує стандартний процес створення та підключення нового розділу (`View` та `ViewModel`) до архітектури StockFlow (.NET 8 WPF, CommunityToolkit.Mvvm).

---

## 1. Створення файлів екрана

Усі компоненти інтерфейсу розміщуються виключно в проєкті `src/StockFlow.UI`.

### 1.1. Модель представлення (ViewModel)

**Шлях:** `src/StockFlow.UI/ViewModels/{Назва}ViewModel.cs`

Вимоги:

- Успадковує базовий клас `ViewModelBase`.
- Залежності (сервіси) приймає через конструктор (DI).
- Асинхронне завантаження даних виконується методом `LoadAsync()`.

Приклад:

```csharp
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StockFlow.UI.ViewModels;

public partial class ExampleViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ExampleViewModel(/* сервіси через DI */)
    {
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        // Завантаження даних у фоновому потоці через Task.Run(...)
        IsLoading = false;
    }
}
```

### 1.2. Розмітка та Code-Behind (View)

**Шляхи:**

- `src/StockFlow.UI/Views/{Назва}View.xaml`
- `src/StockFlow.UI/Views/{Назва}View.xaml.cs`

Файл розмітки `{Назва}View.xaml`:

```xml
<UserControl
    d:DataContext="{d:DesignInstance Type=vm:ExampleViewModel}"
    mc:Ignorable="d"
    x:Class="StockFlow.UI.Views.ExampleView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:StockFlow.UI.ViewModels"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Заголовок розділу -->
        <TextBlock
            FontSize="20"
            FontWeight="Bold"
            Foreground="{StaticResource PrimaryTextBrush}"
            Margin="0,0,0,16"
            Text="Назва розділу"/>

        <!-- Робоча область -->
        <Border
            Background="{StaticResource BackgroundBrush}"
            BorderBrush="{StaticResource BorderBrush}"
            BorderThickness="1"
            Grid.Row="1"
            Padding="16">
            <!-- Таблиці, форми, панелі фільтрів -->
        </Border>
    </Grid>
</UserControl>
```

Файл коду `{Назва}View.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace StockFlow.UI.Views;

public partial class ExampleView : UserControl
{
    public ExampleView()
    {
        InitializeComponent();
    }
}
```

**Важливо — налаштування файлів у Visual Studio:**

- Для `.xaml`: `Build Action = Page`, `Copy to Output Directory = Do not copy`.
- Для `.xaml.cs`: `Build Action = Compile`, `Copy to Output Directory = Do not copy`.

---

## 2. Реєстрація в Dependency Injection (DI)

1. Відкрийте файл `src/StockFlow.UI/App.xaml.cs`.
2. Перейдіть до методу `ConfigureServices(IServiceCollection services)`.
3. Додайте реєстрацію нової моделі представлення як `Transient`:

```csharp
private void ConfigureServices(IServiceCollection services)
{
    // ... реєстрація MainWindow, навігації та наявних екранів ...

    services.AddTransient<ExampleViewModel>();
}
```

**Примітка:** репозиторії та сервіси бізнес-логіки додаються у шар `StockFlow.BusinessLogic` через метод розширення `services.AddStockFlow(configuration)`. UI напряму до `DataAccess` не звертається.

---

## 3. Зв'язування DataTemplate у MainWindow.xaml

Головне вікно програми відображає активний екран через елемент:

```xml
<ContentControl Content="{Binding CurrentViewModel}"/>
```

Щоб WPF знав, який саме `UserControl` підставити для певної `ViewModel`, зареєструйте шаблон у секції `<Window.Resources>` файлу `src/StockFlow.UI/Views/MainWindow.xaml`:

```xml
<Window.Resources>
    <!-- ... наявні конвертери та DataTemplate ... -->

    <DataTemplate DataType="{x:Type vm:ExampleViewModel}">
        <views:ExampleView/>
    </DataTemplate>
</Window.Resources>
```

---

## 4. Навігація та додавання кнопки в бічне меню

### 4.1. Додавання команди в ViewModel головного вікна

Відкрийте файл `src/StockFlow.UI/ViewModels/MainWindowViewModel.cs` та додайте метод переходу з атрибутом `[RelayCommand]`:

```csharp
[RelayCommand]
private void NavigateExample()
{
    Navigation.NavigateTo<ExampleViewModel>();
}
```

### 4.2. Додавання кнопки в меню

Відкрийте файл `src/StockFlow.UI/Views/MainWindow.xaml`, знайдіть контейнер сайдбару та додайте кнопку:

```xml
<Button
    Command="{Binding NavigateExampleCommand}"
    Content="Назва розділу"
    Style="{StaticResource SidebarButton}"/>
```

---

## 5. Обмеження доступу за роллю (лише для Адміністратора)

У системі діють дві ролі: Адміністратор (`Admin`) та Працівник (`Employee`). Операції створення, редагування й видалення дозволені лише Адміністратору.

### 5.1. Приховування елементів в інтерфейсі (UI)

У відповідній `ViewModel` оголошується логічна властивість:

```csharp
public bool CanManage => _currentUserContext.IsAdmin;
```

У розмітці `.xaml` кнопка або форма прив'язується через конвертер видимості:

```xml
<Button
    Command="{Binding CreateCommand}"
    Content="+ Створити"
    Visibility="{Binding CanManage, Converter={StaticResource BooleanToVisibilityConverter}}"/>
```

### 5.2. Обов'язкова валідація на рівні бізнес-сервісів

Приховування елементів у UI є лише візуальним заходом:

- Перевірка ролі обов'язково дублюється всередині відповідного сервісу бізнес-логіки.
- Якщо метод викликано користувачем із роллю `Employee`, сервіс зобов'язаний кидати виняток із повідомленням українською мовою: **«Виконувати цю дію може лише Адміністратор.»**

---

## 6. Використання стилів з Theme.xaml

Усі інтерфейси StockFlow проєктуються за єдиним дизайн-кодом із файлу `src/StockFlow.UI/Styles/Theme.xaml`.

### 6.1. Правило плоского дизайну

- Заборонено використовувати будь-які заокруглення кутів.
- Усі елементи (`Border`, `Button`, `TextBox`, панелі) повинні мати `CornerRadius="0"`.

### 6.2. Системна палітра кольорів

Категорично заборонено задавати кольори безпосередньо (наприклад, `Background="White"`). Використовуйте ресурси теми:

| Ресурс | Шістнадцятковий код | Призначення |
|---|---|---|
| `{StaticResource BackgroundBrush}` | `#FFFFFF` | Основне тло вікон і робочих контейнерів |
| `{StaticResource HeaderBackgroundBrush}` | `#E8E8E4` | Бежево-сіра шапка вікна та заголовки таблиць |
| `{StaticResource SidebarBackgroundBrush}` | `#F1F1EF` | Тло лівого сайдбару навігації |
| `{StaticResource PrimaryTextBrush}` | `#1E293B` | Основний текст і заголовки |
| `{StaticResource SecondaryTextBrush}` | `#64748B` | Підписи полів, підказки, другорядний текст |
| `{StaticResource BorderBrush}` | `#333333` | Межі таблиць, полів вводу та роздільні лінії |

### 6.3. Стандарти оформлення елементів

- **Кнопки дій:** прямокутний стиль `Button` за замовчуванням із рамкою `BorderBrush`.
- **Кнопки меню:** обов'язково зі стилем `Style="{StaticResource SidebarButton}"`.
- **Таблиці:** стандартний `DataGrid` (автоматичні плоскі заголовки `#E8E8E4` та чергування фону рядків).
- **Поля вводу:** `TextBox` із рамкою 1 px без заокруглень.
