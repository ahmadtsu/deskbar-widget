# Attribution

`deskbar-widget` is MIT-licensed (see `LICENSE`). It reuses ideas and adapted
code from two MIT-licensed open-source projects, whose copyright notices are
reproduced below as required by the MIT license. Adapted/translated files also
carry a one-line header pointing here.

## EverythingToolbar — taskbar anchoring, free-gap measurement, theming

- Source: https://github.com/srwi/EverythingToolbar (checked as a read-only
  reference next to this repo; never committed here)
- License: MIT — Copyright (c) 2023 Stephan Rumswinkel
- Also bundles CSDeskBand and other third-party MIT notices; see its `LICENSE`
  for the full list.
- What we adapted for Phase 2: `TaskbarWindow` attach/position logic
  (`WS_CHILD` + `SetParent(Shell_TrayWnd)`), `TaskbarLayoutProbe` UI Automation
  free-gap measurement, `TaskbarWindowAnimator` glide, `TaskbarWindowHost`
  retry/re-dock, `NativeMethods`/`WindowsPolicy` helpers, `ThemeService` /
  `Themed` behavior and Win11 Light/Dark resource tokens (re-skinned to our
  squircle number tiles).

```
MIT License

Copyright (c) 2023 Stephan Rumswinkel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## YASB Reborn — virtual-desktop COM blueprint

- Source: https://github.com/amnweb/yasb (read-only reference; never committed)
- License: MIT — Copyright (c) 2024 amnweb; original Yasb MIT Copyright (c)
  2021 denBot (see its `LICENSE` for the full chain)
- What we translated for Phase 1 (Python → C#): undocumented virtual-desktop
  COM GUIDs, tier-probing fallback (`interfaces.py`), plain-value API wrapper
  (`com.py`), dual-sink push notifications (`notification.py`), cached service
  with coalesced refresh + poll fallback (`service.py`), and the
  `selfcheck.py` diagnostic idea behind our `--selfcheck` mode.

```
MIT License

Copyright (c) 2024 amnweb

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
