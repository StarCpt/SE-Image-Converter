// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h";
#include <iostream>;
#include <thread>;
#include <cstdint>;

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
        case DLL_THREAD_ATTACH:
        case DLL_THREAD_DETACH:
        case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

const int ditherArr[4][3] =
{
    { 1, 0, 7 },
    {-1, 1, 3 },
    { 0, 1, 5 },
    { 1, 1, 1 },
};

//premultiplied values
int preMultipliedTable[4][2] =
{
    { 3, 0 },
    {-3, 0 },
    { 0, 0 },
    { 3, 0 },
};

const float preMultipliedArr[4] = { 0.4375f, 0.1875f, 0.3125f, 0.0625f };

std::byte ChangeBitDepthFast(int singleChannelColor, double colorStepInterval)
{
    return (std::byte)std::clamp(std::round(singleChannelColor / colorStepInterval) * colorStepInterval, 0.01, 254.99);
}

const int ditherIterations = 4;
const int colorChannels = 3;
static int realWidth;// = width * colorChannels;
static int strideDiff;// = imgStride - realWidth;
static int imgByteSizeMinusStrideDiff;// = imgByteSize - strideDiff;
static int widthMinusOne;// = width - 1;

int DitherThread(std::byte colorArr[], int imgByteSize, int imgStride, double colorStepInterval, int bigColorArr[], int channel)
{
    for (int c = channel; c < imgByteSize;)
    {
        int oldColor = bigColorArr[c] + (int)colorArr[c];
        colorArr[c] = ChangeBitDepthFast(oldColor, colorStepInterval);

        for (int i = 0; i < ditherIterations; i++)
        {
            int offsetPos = c + preMultipliedTable[i][1];
            int offsetPosX = (c % imgStride / 3) + ditherArr[i][1];

            if (!(offsetPos >= imgByteSizeMinusStrideDiff || offsetPos < 0) &&
                !(offsetPosX < 0) &&
                !(offsetPosX > widthMinusOne))
            {
                bigColorArr[offsetPos] += (int)std::round((oldColor - (int)colorArr[c]) * preMultipliedArr[i]);
            }
        }

        c += colorChannels;
        if ((c - channel) % imgStride == realWidth)
        {
            c += strideDiff;
        }
    }

    return 0;
}

extern "C" __declspec(dllexport) int DitherCPP(std::byte colorArr[], int imgByteSize, int width, int imgStride, double colorStepInterval)
{
    int* bigColorArr = new int[imgByteSize];
    for (int i = 0; i < imgByteSize; i++)
    {
        bigColorArr[i] = 0;
    }

    realWidth = width * colorChannels;
    strideDiff = imgStride - realWidth;
    imgByteSizeMinusStrideDiff = imgByteSize - strideDiff;
    widthMinusOne = width - 1;

    preMultipliedTable[0][1] = (ditherArr[0][1] * imgStride) + preMultipliedTable[0][0];
    preMultipliedTable[1][1] = (ditherArr[1][1] * imgStride) + preMultipliedTable[1][0];
    preMultipliedTable[2][1] = (ditherArr[2][1] * imgStride) + preMultipliedTable[2][0];
    preMultipliedTable[3][1] = (ditherArr[3][1] * imgStride) + preMultipliedTable[3][0];

    //color format: BGR24 / Blue Green Red
    std::thread BlueThread(DitherThread, colorArr, imgByteSize, imgStride, colorStepInterval, bigColorArr, 0);
    std::thread GreenThread(DitherThread, colorArr, imgByteSize, imgStride, colorStepInterval, bigColorArr, 1);
    std::thread RedThread(DitherThread, colorArr, imgByteSize, imgStride, colorStepInterval, bigColorArr, 2);

    BlueThread.join();
    GreenThread.join();
    RedThread.join();

    return 0;
}