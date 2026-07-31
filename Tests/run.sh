#!/usr/bin/env bash
# Компилирует ядро и Unity-адаптер вне редактора и гоняет тесты.
# Async-сборка требует UniTask и в харнесс не входит.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PKG="$ROOT/Packages/com.exerussus.di"
OUT="$ROOT/Tests/bin"

mkdir -p "$OUT"

# Git Bash отдаёт пути вида /c/..., нативные Windows-программы их не понимают.
to_native() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}

# ---------- doccheck ----------
# Ищем рабочий Python 3: под Windows python3 нередко оказывается заглушкой Store,
# поэтому проверяем не наличие команды, а то, что она реально запускается.
PYTHON=""
for candidate in python3 python py; do
  command -v "$candidate" >/dev/null 2>&1 || continue
  args=""
  [ "$candidate" = "py" ] && args="-3"
  if "$candidate" $args -c "import sys; sys.exit(0 if sys.version_info[0] == 3 else 1)" </dev/null >/dev/null 2>&1; then
    PYTHON="$candidate $args"
    break
  fi
done

if [ -n "$PYTHON" ]; then
  $PYTHON "$(to_native "$ROOT/Tests/doccheck.py")"
elif [ "${DOCCHECK_REQUIRED:-0}" = "1" ]; then
  echo "doccheck: рабочий Python 3 не найден, а DOCCHECK_REQUIRED=1" >&2
  exit 1
else
  echo "doccheck: пропущен, рабочий Python 3 не найден (в CI ставьте DOCCHECK_REQUIRED=1)" >&2
fi

# ---------- сборка ----------
SOURCES=()
while IFS= read -r file; do SOURCES+=("$file"); done < <(
  find "$PKG/Runtime/Core" "$PKG/Runtime/Unity" -name '*.cs' | sort
)
SOURCES+=("$ROOT/Tests/stubs.cs" "$ROOT/Tests/tests.cs")

if command -v dotnet >/dev/null 2>&1; then
  PROJECT="$OUT/harness.csproj"
  {
    echo '<Project Sdk="Microsoft.NET.Sdk">'
    echo '  <PropertyGroup>'
    echo '    <OutputType>Exe</OutputType>'
    echo "    <TargetFramework>${TARGET_FRAMEWORK:-net8.0}</TargetFramework>"
    # LangVersion 9 — потолок Unity 2021.3. Ловит случайное использование более новых фич.
    echo '    <LangVersion>9</LangVersion>'
    echo '    <Nullable>disable</Nullable>'
    echo '    <AssemblyName>DiTests</AssemblyName>'
    echo '    <RootNamespace>Exerussus.DI.Tests</RootNamespace>'
    echo '    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
    echo '    <NoWarn>0649;0169;0414</NoWarn>'
    echo '  </PropertyGroup>'
    echo '  <ItemGroup>'
    for file in "${SOURCES[@]}"; do echo "    <Compile Include=\"$(to_native "$file")\" />"; done
    echo '  </ItemGroup>'
    echo '</Project>'
  } > "$PROJECT"

  exec dotnet run --project "$(to_native "$PROJECT")" -v quiet
fi

if command -v mcs >/dev/null 2>&1 && command -v mono >/dev/null 2>&1; then
  mcs -langversion:latest -nowarn:649,169,414 -out:"$OUT/DiTests.exe" "${SOURCES[@]}"
  exec mono "$OUT/DiTests.exe"
fi

echo "нужен dotnet SDK или mcs+mono" >&2
exit 1
