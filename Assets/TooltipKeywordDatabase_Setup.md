# Tooltip Keyword Database Setup

`TooltipKeywordFormatter` now loads keyword definitions from:

- `Resources/UI/TooltipKeywordDatabase.asset`

## Create the asset

1. In Unity Project window, create folder if missing:
   - `Assets/Resources/UI`
2. Right click in Project window:
   - `Create > Dice Orbit > UI > Tooltip Keyword Database`
3. Save it as:
   - `TooltipKeywordDatabase.asset`
4. Place it under:
   - `Assets/Resources/UI/TooltipKeywordDatabase.asset`

## Edit entries

Each entry supports:

- `key`: keyword text (e.g. `집중`)
- `description`: detail text shown in tooltip/detail panel
- `color`: keyword highlight color
- `icon`: reserved for future keyword icon rendering

If no asset is found, formatter falls back to built-in default keywords.
