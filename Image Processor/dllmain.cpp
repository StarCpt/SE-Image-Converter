// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h";
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

const float preMultipliedArr[4] = { 0.4375f, 0.1875f, 0.3125f, 0.0625f };

const int32_t ditherIterations = 4;
const int32_t colorChannels = 3;
static int32_t realWidth;// = width * colorChannels;
static int32_t strideDiff;// = imgStride - realWidth;
static int32_t imgByteSizeMinusStrideDiff;// = imgByteSize - strideDiff;

int DitherThread(uint8_t colorArr[], int32_t imgByteSize, int32_t width, int32_t imgStride, double colorStepInterval, int32_t bigColorArr[], int32_t channel)
{
    for (int c = channel; c < imgByteSize - strideDiff;)
    {
        int32_t oldColor = bigColorArr[c] + colorArr[c];
        colorArr[c] = ChangeBitDepthFastCPP(oldColor, colorStepInterval);
        int32_t error = oldColor - colorArr[c];

        for (int i = 0; i < ditherIterations; i++)
        {
            int32_t offsetPos = c + preCalcTable[i][1];
            int32_t offsetPosX = (c % imgStride / colorChannels) + ditherArr[i][1];

            if (!(offsetPos >= imgByteSize - strideDiff || offsetPos < 0) &&
                !(offsetPosX < 0) &&
                !(offsetPosX >= width))
            {
                bigColorArr[offsetPos] += (int32_t)std::nearbyint((oldColor - colorArr[c]) * preMultipliedArr[i]);
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

extern "C" __declspec(dllexport) uint8_t ChangeBitDepthFastCPP(int32_t singleChannelColor, double_t colorStepInterval)
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

extern "C" __declspec(dllexport) int32_t DitherThread3(uint8_t colorArr[], int32_t arrSize, int32_t bigColorArr[], int32_t colorChannels, int32_t channel, int32_t width, int32_t stride, double_t colorStepInterval)
{
    int32_t ditherIterations = 4;
    int32_t realWidth = width * colorChannels;
    int32_t strideDiff = stride - realWidth;

    for (int32_t c = channel; c < arrSize - strideDiff;)
    {
        int32_t oldColor = bigColorArr[c] + colorArr[c];
        colorArr[c] = ChangeBitDepthFastCPP(oldColor, colorStepInterval);
        int32_t error = oldColor - colorArr[c];

        for (int32_t i = 0; i < ditherIterations; i++)
        {
            int32_t offsetPos = c + (stride * ditherArr[i][1]) + (ditherArr[i][0] * colorChannels);
            int32_t offsetPosX = (c % stride / colorChannels) + ditherArr[i][1];
            bool outOfRange = offsetPos >= arrSize - strideDiff || offsetPos < 0;
            bool beforeOrAfterWidth = (offsetPosX < 0) || (offsetPosX > width - 1);

            if (!outOfRange && !beforeOrAfterWidth)
            {
                bigColorArr[offsetPos] += (int32_t)nearbyint(error * ditherArr[i][2] / 16.0f);
            }
        }

        c += colorChannels;
        if ((c - channel) % stride == realWidth)
        {
            c += strideDiff;
        }
    }

    return 0;
}

extern "C" __declspec(dllexport) int32_t DitherThread4(uint8_t colorArr[], int32_t bigColorArr[], int32_t channel)
{
    for (int32_t c = channel; c < 95406;)
    {
        int32_t oldColor = bigColorArr[c] + (int32_t)colorArr[c];
        colorArr[c] = ChangeBitDepthFastCPP(oldColor, 36.428571428571431);
        int32_t error = oldColor - (int32_t)colorArr[c];

        for (int32_t i = 0; i < 4; i++)
        {
            int32_t offsetPos = c + (536 * ditherArr[i][1]) + (ditherArr[i][0] * 3);
            int32_t offsetPosX = (c % 536 / 3) + ditherArr[i][1];
            bool outOfRange = offsetPos >= 95406 || offsetPos < 0;
            bool beforeOrAfterWidth = (offsetPosX < 0) || (offsetPosX > 177);

            if (!outOfRange && !beforeOrAfterWidth)
            {
                /*int one = ditherArr[i][2];
                int two = error * ditherArr[i][2];
                double three = error * ditherArr[i][2] / 16.0;
                double four = round(error * ditherArr[i][2] / 16.0);
                int five = (int32_t)round(error * ditherArr[i][2] / 16.0);
                int six = bigColorArr[offsetPos];*/
                bigColorArr[offsetPos] += (int32_t)nearbyint(error * ditherArr[i][2] / 16.0);
            }
        }

        c += 3;
        if ((c - channel) % 536 == 534)
        {
            c += 2;
        }
    }

    return 0;
}

extern "C" __declspec(dllexport) int ChangeBitDepthAndDitherFastThreadedCPP(std::byte colorArr[], int imgByteSize, int width, int imgStride, int colorDepth)
{
    int* bigColorArr = new int[imgByteSize];
    for (int i = 0; i < imgByteSize; i++)
    {
        bigColorArr[i] = 0;
    }

    double colorStepInterval = 255.0 / (std::pow(2, colorDepth) - 1.0);

    realWidth = width * colorChannels;
    strideDiff = imgStride - realWidth;
    imgByteSizeMinusStrideDiff = imgByteSize - strideDiff;

    preCalcTable[0][1] = (ditherArr[0][1] * imgStride) + preCalcTable[0][0];
    preCalcTable[1][1] = (ditherArr[1][1] * imgStride) + preCalcTable[1][0];
    preCalcTable[2][1] = (ditherArr[2][1] * imgStride) + preCalcTable[2][0];
    preCalcTable[3][1] = (ditherArr[3][1] * imgStride) + preCalcTable[3][0];

    ////color format: BGR24 / Blue Green Red
    std::thread BlueThread(DitherThread, colorArr, imgByteSize, imgStride, colorStepInterval, bigColorArr, 0);
    std::thread GreenThread(DitherThread, colorArr, imgByteSize, imgStride, colorStepInterval, bigColorArr, 1);
    std::thread RedThread(DitherThread, colorArr, imgByteSize, imgStride, colorStepInterval, bigColorArr, 2);

    BlueThread.join();
    GreenThread.join();
    RedThread.join();

    return 0;
}

//extern "C" __declspec(dllexport) int32_t ChangeBitDepthAndDitherFastThreadedCPP(uint8_t colorArr[], int32_t imgByteSize, int32_t width, int32_t imgStride, int32_t colorDepth)
//{
//    int32_t* bigColorArr = new int32_t[imgByteSize];
//    for (int32_t i = 0; i < imgByteSize; i++)
//    {
//        bigColorArr[i] = 0;
//    }
//
//    double colorStepInterval = 255.0 / (pow(2, colorDepth) - 1);
//
//    /*DitherThread4(colorArr, imgByteSize, bigColorArr, 3, 0, width, imgStride, colorStepInterval);
//    DitherThread4(colorArr, imgByteSize, bigColorArr, 3, 1, width, imgStride, colorStepInterval);
//    DitherThread4(colorArr, imgByteSize, bigColorArr, 3, 2, width, imgStride, colorStepInterval);*/
//
//    return 0;
//}