// SobelAmp.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "amp.h"
#include <amp_math.h>

using namespace concurrency;
using namespace concurrency::fast_math;

int grayscale(int pixel) restrict(amp)
{
	int red, green, blue, grey;

	red = (pixel & 0x00ff0000) >> 16;
	green = (pixel & 0x0000ff00) >> 8;
	blue = (pixel & 0x000000ff);

	//grey = (int)(luminance * 0.3) + (int)(green * 0.59) + (int)(blue * 0.11);
	float sum = 0.241 * red*red + 0.691 * green*green + 0.068 * blue*blue;
	grey =  sqrt(sum);

	pixel = (pixel & 0xff000000) | (grey << 16) | (grey << 8) | grey;

	return pixel;
}

int luminance(int pixel) restrict(amp)
{
	int red, green, blue, lum;

	red = (pixel & 0x00ff0000) >> 16;
	green = (pixel & 0x0000ff00) >> 8;
	blue = (pixel & 0x000000ff);

	float sum = 0.241 * red*red + 0.691 * green*green + 0.068 * blue*blue;
	lum =  sqrt(sum);

	return lum;
}

extern "C" __declspec ( dllexport ) void _stdcall filter_image(unsigned int* imageData, unsigned int* outputData, int width, int height, double threshold)
{
	array_view<unsigned int, 2> img(height, width, imageData);
	array_view<unsigned int, 2> result(height, width, outputData);
	result.discard_data(); // write-only buffer for the output

	parallel_for_each(         
		img.extent,         
		[=](index<2> idx) restrict(amp)
	{
		int i = idx[0];
		int j = idx[1];

		int cr = img(i + 1, j);			// right
		int cl = img(i - 1, j);			// left
		int cu = img(i, j - 1);			// up
		int cd = img(i, j + 1);			// down
		int cld = img(i - 1, j + 1);	// left-down
		int clu = img(i - 1, j - 1);	// left-up
		int crd = img(i + 1, j + 1);	// right-down
		int cru = img(i + 1, j - 1);	// right-up

		int dx = 0, dy = 0;
		dx = luminance(cld) + 2 * luminance(cd) + luminance(crd) - (luminance(clu) + 2 * luminance(cu) + luminance(cru));
		dy = luminance(crd) + 2 * luminance(cr) + luminance(cru) - (luminance(cld) + 2 * luminance(cl) + luminance(clu));

		float power = fabsf(dx) + fabsf(dy);

		if(power < threshold)
		{
			result(i,j) = 0xffffffff;
		}
		else
		{
			result(i,j) = 0xff101010;
		}
	});

	img.synchronize();
}
