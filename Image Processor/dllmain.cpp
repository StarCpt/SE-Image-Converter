// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <iostream>
#include <thread>
#include <cstdint>

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

const int32_t ditherArr[4][3] =
{
    { 1, 0, 7 },
    {-1, 1, 3 },
    { 0, 1, 5 },
    { 1, 1, 1 },
};

int32_t preCalcTable[4][2] =
{
    { 3, 0 },
    {-3, 0 },
    { 0, 0 },
    { 3, 0 },
};

const float preCalcArr[4] = { 0.4375f, 0.1875f, 0.3125f, 0.0625f };

const int32_t ditherIterations = 4;
const int32_t colorChannels = 3;

uint8_t ChangeBitDepthFast(int32_t singleChannelColor, double_t colorStepInterval)
{
    return (uint8_t)std::clamp(nearbyint(singleChannelColor / colorStepInterval) * colorStepInterval, 0.0, 255.0);
}

extern "C" __declspec(dllexport) int32_t ChangeBitDepthCPP(uint8_t colorArr[], int32_t arrayLength, int32_t colorDepth)
{
    double colorStepInterval = 255.0 / ((pow(2, colorDepth)) - 1);

    for (int32_t i = 0; i < arrayLength; i++)
    {
        colorArr[i] = (uint8_t)std::clamp(nearbyint(colorArr[i] / colorStepInterval) * colorStepInterval, 0.0, 255.0);
    }

    return 0;
}

int32_t DitherThread(uint8_t colorArr[], int32_t width, int32_t realWidth, int32_t imgStride, int32_t strideDiff, int32_t arrSizeMinusStrideDiff, double colorStepInterval, int32_t bigColorArr[], int32_t channel)
{
    for (int c = channel; c < arrSizeMinusStrideDiff;)
    {
        int32_t oldColor = bigColorArr[c] + colorArr[c];
        colorArr[c] = ChangeBitDepthFast(oldColor, colorStepInterval);
        int32_t error = oldColor - colorArr[c];

        for (int i = 0; i < ditherIterations; i++)
        {
            int32_t offsetPos = c + preCalcTable[i][1];
            int32_t offsetPosX = (c % imgStride / colorChannels) + ditherArr[i][1];

            if (!(offsetPos >= arrSizeMinusStrideDiff || offsetPos < 0) &&
                !(offsetPosX < 0 || offsetPosX >= width))
            {
                bigColorArr[offsetPos] += (int32_t)std::nearbyint(error * preCalcArr[i]);
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

extern "C" __declspec(dllexport) int ChangeBitDepthAndDitherFastThreadedCPP(uint8_t colorArr[], int imgByteSize, int width, int imgStride, int colorDepth)
{
    int32_t* bigColorArr = new int32_t[imgByteSize];
    for (int32_t i = 0; i < imgByteSize; i++)
    {
        bigColorArr[i] = 0;
    }

    double colorStepInterval = 255.0 / (std::pow(2, colorDepth) - 1);

    int32_t realWidth = width * colorChannels;
    int32_t strideDiff = imgStride - realWidth;
    int32_t arrSizeMinusStrideDiff = imgByteSize - strideDiff;

    preCalcTable[0][1] = (ditherArr[0][1] * imgStride) + preCalcTable[0][0];
    preCalcTable[1][1] = (ditherArr[1][1] * imgStride) + preCalcTable[1][0];
    preCalcTable[2][1] = (ditherArr[2][1] * imgStride) + preCalcTable[2][0];
    preCalcTable[3][1] = (ditherArr[3][1] * imgStride) + preCalcTable[3][0];

    //color format: BGR24 / Blue Green Red
    std::thread BlueThread(DitherThread, colorArr, width, realWidth, imgStride, strideDiff, arrSizeMinusStrideDiff, colorStepInterval, bigColorArr, 0);
    std::thread GreenThread(DitherThread, colorArr, width, realWidth, imgStride, strideDiff, arrSizeMinusStrideDiff, colorStepInterval, bigColorArr, 1);
    std::thread RedThread(DitherThread, colorArr, width, realWidth, imgStride, strideDiff, arrSizeMinusStrideDiff, colorStepInterval, bigColorArr, 2);

    BlueThread.join();
    GreenThread.join();
    RedThread.join();

    return 0;
}