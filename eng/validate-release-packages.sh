#!/usr/bin/env bash
set -euo pipefail

package_directory="${1:?Usage: validate-release-packages.sh <package-directory> <version> [readme]}"
package_version="${2:?Usage: validate-release-packages.sh <package-directory> <version> [readme]}"
readme_file="${3:-README.md}"

if [[ ! -d "${package_directory}" ]]; then
  echo "Package directory does not exist: ${package_directory}" >&2
  exit 1
fi

if [[ ! -f "${readme_file}" ]]; then
  echo "README does not exist: ${readme_file}" >&2
  exit 1
fi

audit_directory=$(mktemp -d)
trap 'rm -rf "${audit_directory}"' EXIT

shopt -s nullglob
packages=("${package_directory}"/*.nupkg)
symbol_packages=("${package_directory}"/*.snupkg)

if [[ ${#packages[@]} -ne 48 ]]; then
  echo "Expected 48 NuGet packages, found ${#packages[@]}." >&2
  exit 1
fi

if [[ ${#symbol_packages[@]} -ne 48 ]]; then
  echo "Expected 48 symbol packages, found ${#symbol_packages[@]}." >&2
  exit 1
fi

package_ids_file="${audit_directory}/package-ids.txt"
readme_links_file="${audit_directory}/readme-links.txt"
readme_badges_file="${audit_directory}/readme-badges.txt"
: > "${package_ids_file}"

manifest_count=0
for package in "${packages[@]}"; do
  entries=$(unzip -Z1 "${package}")
  nuspec=$(printf '%s\n' "${entries}" | grep -E '\.nuspec$')

  if [[ -z "${nuspec}" || $(printf '%s\n' "${nuspec}" | wc -l) -ne 1 ]]; then
    echo "Expected one nuspec in ${package}." >&2
    exit 1
  fi

  metadata=$(unzip -p "${package}" "${nuspec}")
  package_id=$(printf '%s' "${metadata}" | sed -n 's:.*<id>\([^<]*\)</id>.*:\1:p' | head -n 1)

  if [[ -z "${package_id}" ]]; then
    echo "Package ID is missing in ${package}." >&2
    exit 1
  fi

  printf '%s\n' "${package_id}" >> "${package_ids_file}"
  printf '%s\n' "${entries}" | grep -Fxq 'README.md'
  printf '%s\n' "${entries}" | grep -Fxq 'app-icon.png'
  printf '%s' "${metadata}" | grep -Fq "<version>${package_version}</version>"
  printf '%s' "${metadata}" | grep -Fq '<license type="expression">MIT</license>'
  printf '%s' "${metadata}" | grep -Fq '<readme>README.md</readme>'
  printf '%s' "${metadata}" | grep -Fq '<icon>app-icon.png</icon>'
  printf '%s' "${metadata}" | grep -Fq '<repository type="git" url="https://github.com/wieslawsoltes/XamlVisualEditor"'
  printf '%s' "${metadata}" | grep -Fq "<releaseNotes>https://github.com/wieslawsoltes/XamlVisualEditor/releases/tag/v${package_version}</releaseNotes>"

  if printf '%s\n' "${entries}" | grep -Fxq 'xve.extension.json'; then
    unzip -p "${package}" xve.extension.json | jq -e --arg version "${package_version}" '
      .name != "" and
      .displayName != "" and
      .publisher == "wieslawsoltes" and
      .version == $version and
      .engines.xve == ("^" + $version) and
      (.main | startswith("lib/net10.0/"))
    ' > /dev/null
    manifest_count=$((manifest_count + 1))
  fi
done

if [[ ${manifest_count} -ne 21 ]]; then
  echo "Expected 21 built-in extension manifests, found ${manifest_count}." >&2
  exit 1
fi

sort -u "${package_ids_file}" -o "${package_ids_file}"
grep -Eo 'https://www\.nuget\.org/packages/[^/) ]+' "${readme_file}" \
  | sed 's#https://www.nuget.org/packages/##' \
  | sort -u > "${readme_links_file}"
grep -Eo 'https://img\.shields\.io/nuget/v/[^?) ]+' "${readme_file}" \
  | sed -e 's#https://img.shields.io/nuget/v/##' -e 's#\.svg$##' \
  | sort -u > "${readme_badges_file}"

if ! diff -u "${package_ids_file}" "${readme_links_file}"; then
  echo "README NuGet links do not match the produced package IDs." >&2
  exit 1
fi

if ! diff -u "${package_ids_file}" "${readme_badges_file}"; then
  echo "README NuGet badges do not match the produced package IDs." >&2
  exit 1
fi

echo "Validated 48 NuGet packages, 48 symbol packages, 21 extension manifests, and README package badges for ${package_version}."
