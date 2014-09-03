// NativeAmpLibrary.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "amp.h"

using namespace concurrency;

extern "C" __declspec ( dllexport ) void _stdcall square_array(float* arr, int n)
{
	// Create a view over the data on the CPU
	array_view<float,1> dataView(n, &arr[0]);

	// Run code on the GPU
	parallel_for_each(dataView.extent, [=] (index<1> idx) restrict(amp)
	{
		dataView[idx] = dataView[idx] * dataView[idx];
	});

	// Copy data from GPU to CPU
	dataView.synchronize();
}
