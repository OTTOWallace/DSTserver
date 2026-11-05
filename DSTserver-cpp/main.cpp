// DSTserver - Win32 窗口程序入口
// 生成环境: Visual Studio 2026 / MSVC 14.51 / Windows SDK 10.0.26100

#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif

#include <windows.h>

#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")

namespace
{
    constexpr wchar_t kWindowClass[] = L"DSTserverWindowClass";
    constexpr wchar_t kWindowTitle[] = L"DSTserver";

    constexpr int kDefaultWidth  = 960;
    constexpr int kDefaultHeight = 640;

    HBRUSH g_backgroundBrush = nullptr;

    LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
        case WM_CREATE:
            return 0;

        case WM_SIZE:
            InvalidateRect(hWnd, nullptr, TRUE);
            return 0;

        case WM_PAINT:
        {
            PAINTSTRUCT ps{};
            HDC hdc = BeginPaint(hWnd, &ps);

            RECT rc{};
            GetClientRect(hWnd, &rc);

            // 浅色背景
            FillRect(hdc, &rc, g_backgroundBrush);

            SetBkMode(hdc, TRANSPARENT);
            SetTextColor(hdc, RGB(32, 32, 32));

            // 主标题
            RECT titleRect = rc;
            titleRect.top = rc.bottom / 2 - 60;
            titleRect.bottom = titleRect.top + 48;

            HFONT titleFont = CreateFontW(
                -32, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
                DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
            HGDIOBJ oldFont = SelectObject(hdc, titleFont);
            DrawTextW(hdc, L"DSTserver", -1, &titleRect, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
            SelectObject(hdc, oldFont);
            DeleteObject(titleFont);

            // 副标题
            RECT subRect = rc;
            subRect.top = rc.bottom / 2 - 4;
            subRect.bottom = subRect.top + 28;

            HFONT subFont = CreateFontW(
                -16, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
            oldFont = SelectObject(hdc, subFont);
            SetTextColor(hdc, RGB(110, 110, 110));
            DrawTextW(hdc, L"Win32 窗口程序 · Visual Studio 2026 · MSVC 14.51",
                -1, &subRect, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
            SelectObject(hdc, oldFont);
            DeleteObject(subFont);

            EndPaint(hWnd, &ps);
            return 0;
        }

        case WM_ERASEBKGND:
            return 1;   // 背景由 WM_PAINT 负责

        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;

        default:
            break;
        }
        return DefWindowProcW(hWnd, message, wParam, lParam);
    }
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, PWSTR, int nCmdShow)
{
    // 高分屏下保持清晰
    ::SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    g_backgroundBrush = CreateSolidBrush(RGB(246, 247, 249));

    WNDCLASSEXW wc{};
    wc.cbSize        = sizeof(WNDCLASSEXW);
    wc.style         = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc   = WndProc;
    wc.hInstance     = hInstance;
    wc.hCursor       = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = g_backgroundBrush;
    wc.lpszClassName = kWindowClass;

    if (!RegisterClassExW(&wc))
    {
        MessageBoxW(nullptr, L"注册窗口类失败", L"DSTserver", MB_ICONERROR);
        return 0;
    }

    // 居中显示
    RECT workArea{};
    SystemParametersInfoW(SPI_GETWORKAREA, 0, &workArea, 0);
    const int width  = kDefaultWidth;
    const int height = kDefaultHeight;
    const int x = workArea.left + ((workArea.right - workArea.left) - width) / 2;
    const int y = workArea.top  + ((workArea.bottom - workArea.top) - height) / 2;

    HWND hWnd = CreateWindowExW(
        0,
        kWindowClass,
        kWindowTitle,
        WS_OVERLAPPEDWINDOW,
        x, y, width, height,
        nullptr, nullptr, hInstance, nullptr);

    if (!hWnd)
    {
        MessageBoxW(nullptr, L"创建窗口失败", L"DSTserver", MB_ICONERROR);
        return 0;
    }

    ShowWindow(hWnd, nCmdShow);
    UpdateWindow(hWnd);

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    if (g_backgroundBrush)
    {
        DeleteObject(g_backgroundBrush);
        g_backgroundBrush = nullptr;
    }

    return static_cast<int>(msg.wParam);
}
