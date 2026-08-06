# Установщик Mate

Установщик собирается в два этапа: .NET публикует self-contained версию приложения,
затем Inno Setup упаковывает её в один `Setup.exe`.

## Подготовка

1. Установите [Inno Setup](https://jrsoftware.org/isdl.php).
2. Откройте PowerShell в корне проекта.

## Сборка

```powershell
.\build-installer.ps1
```

Для другой версии:

```powershell
.\build-installer.ps1 -Version 1.1.0
```

Готовый файл появится здесь:

```text
artifacts\installer\Mate-Setup-1.0.0.exe
```

Установщик ставит приложение в профиль текущего пользователя, добавляет ярлык в меню «Пуск»,
предлагает создать ярлык на рабочем столе и добавляет обычное удаление приложения через параметры Windows.
