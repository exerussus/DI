#!/usr/bin/env python3
"""Проверки консистентности пакета: версия, ссылка установки, чистота ядра, покрытие API в README."""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PKG = ROOT / "Packages" / "com.exerussus.di"
PKG_README = PKG / "README.md"
ROOT_README = ROOT / "README.md"
CHANGELOG = PKG / "CHANGELOG.md"
MANIFEST = PKG / "package.json"

# Внутренняя механика: в README ей делать нечего.
UNDOCUMENTED_OK = {
    "InjectionMemberKind",
    "MissingDependencyAction",
    "InjectionPoint",
    "DependencyContainerBase",
    "TypeNameUtility",
    "DependencyPromiseRegistry",
}

TYPE_PATTERN = re.compile(
    r"^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|readonly\s+|partial\s+)*"
    r"(?:class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)

COMMENT_PATTERN = re.compile(r"/\*.*?\*/|//[^\n]*", re.DOTALL)

errors = []


def strip_comments(text):
    """Комментарии не считаются ссылкой на движок: в них про UnityEngine писать можно."""
    return COMMENT_PATTERN.sub("", text)


def check_version():
    version = json.loads(MANIFEST.read_text(encoding="utf-8"))["version"]
    heading = re.search(r"^##\s*\[([0-9]+\.[0-9]+\.[0-9]+)\]", CHANGELOG.read_text(encoding="utf-8"), re.MULTILINE)
    if not heading:
        errors.append("CHANGELOG: не найден заголовок версии вида '## [x.y.z]'")
    elif heading.group(1) != version:
        errors.append(f"версии разошлись: package.json {version}, CHANGELOG {heading.group(1)}")


def check_install_url():
    expected = f"?path=/Packages/{PKG.name}"
    for readme in (PKG_README, ROOT_README):
        text = readme.read_text(encoding="utf-8")
        if expected not in text:
            errors.append(f"{readme.relative_to(ROOT)}: нет ссылки установки с '{expected}'")
        if "git URL" not in text and "manifest.json" not in text:
            errors.append(f"{readme.relative_to(ROOT)}: не описана установка через git-ссылку")


def check_core_is_engine_free():
    core = PKG / "Runtime" / "Core"
    for source in core.rglob("*.cs"):
        if "UnityEngine" in strip_comments(source.read_text(encoding="utf-8")):
            errors.append(f"{source.relative_to(ROOT)}: ядро не должно ссылаться на UnityEngine")

    asmdef = json.loads((core / "Exerussus.DI.asmdef").read_text(encoding="utf-8"))
    if not asmdef.get("noEngineReferences"):
        errors.append("Exerussus.DI.asmdef: ожидается noEngineReferences: true")
    if asmdef.get("references"):
        errors.append("Exerussus.DI.asmdef: у ядра не должно быть ссылок на другие сборки")


def check_public_api_documented():
    readme = PKG_README.read_text(encoding="utf-8")
    for source in (PKG / "Runtime").rglob("*.cs"):
        for name in TYPE_PATTERN.findall(strip_comments(source.read_text(encoding="utf-8"))):
            if name in UNDOCUMENTED_OK:
                continue
            mention = name[:-9] if name.endswith("Attribute") else name
            if mention not in readme:
                errors.append(f"README: публичный тип {name} нигде не упомянут")


def check_harness_is_hidden_from_unity():
    """Если репо лежит внутри Unity-проекта, харнесс попадёт в компиляцию и сломает её:
    заглушка UnityEngine.Debug конфликтует с настоящей, а tests.cs уедет в плеерный билд."""
    guard = "#if !UNITY_2020_3_OR_NEWER"
    for source in (ROOT / "Tests").glob("*.cs"):
        text = source.read_text(encoding="utf-8")
        if not text.lstrip().startswith(guard):
            errors.append(f"{source.relative_to(ROOT)}: файл харнесса должен начинаться с '{guard}'")
        elif "#endif" not in text:
            errors.append(f"{source.relative_to(ROOT)}: не закрыт '{guard}'")


def check_no_test_mentions():
    # Тесты — наша внутренняя кухня, в поставляемый пакет они просачиваться не должны.
    pattern = re.compile(r"тест|Test", re.IGNORECASE)
    for source in list(PKG.rglob("*.cs")) + list(PKG.rglob("*.md")) + list(PKG.rglob("*.json")):
        for number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            if pattern.search(line):
                errors.append(f"{source.relative_to(ROOT)}:{number}: упоминание тестов в пакете")


check_version()
check_install_url()
check_core_is_engine_free()
check_public_api_documented()
check_harness_is_hidden_from_unity()
check_no_test_mentions()

if errors:
    print("doccheck: провалено")
    for error in errors:
        print(f"  {error}")
    sys.exit(1)

print("doccheck: ok")
