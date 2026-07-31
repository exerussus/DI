# Exerussus.DI

DI-контейнер для Unity: инъекция по атрибутам на полях и свойствах, скоупы, потокобезопасный
контейнер с ожиданием регистраций.

## Установка

Пакет лежит в подпапке репозитория, поэтому в git-ссылке нужен параметр `?path=`.

Unity Package Manager → **+** → *Add package from git URL*:

```
https://github.com/exerussus/DI.git?path=/Packages/com.exerussus.di
```

С закреплением на версии — тег идёт после `?path=`, через `#`:

```
https://github.com/exerussus/DI.git?path=/Packages/com.exerussus.di#v2.0.0
```

Требуется Unity 2021.3+ и git в `PATH`.

Документация: [Packages/com.exerussus.di/README.md](Packages/com.exerussus.di/README.md)
