# MystTiq — simple Palworld-inspired Ribbon icons

Ten original vector icons for the actions in the supplied screenshot. Capture-sphere motifs identify lifecycle commands; a storage chest, Palbox-style terminal, Pal ears and workbench bring in the survival/crafting theme. Emergency Force Stop keeps the conventional red octagon and cross for immediate recognition.

24×24 design grid; 1.8-unit rounded strokes. Recommended display size: 24–28 DIP. All are single-color, transparent vectors. No AI raster generation or external fonts are required.

Files: SVG per icon; icons.json with geometry, labels and dark/light colors; PalRibbonIcons.axaml with equivalent Avalonia geometries; preview.html; dark/light PNG previews rendered from the same vector paths.

Avalonia integration: include the ResourceDictionary, then use Path with Data={StaticResource PalRibbon_start}, Width=24, Height=24, Stretch=Uniform, StrokeThickness=1.8, StrokeLineCap=Round, StrokeJoin=Round, and a theme-aware Stroke brush. Leave Fill unset. Keep the existing command, label, accessibility name and enabled-state logic. Apply disabled opacity to the whole button; color alone must not indicate availability.

SVGs use currentColor for inline integration. When used through an HTML img tag, assign a fixed stroke color to the SVG or embed it inline; parent CSS color does not cross an external image boundary.

This is a design/replacement asset set. The v0.7.110.1 application downloads have not been changed by this design pass.
