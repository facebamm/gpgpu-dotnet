#include <pch.h>
#include <amp.h>
#include <ppltasks.h>
#include <collection.h>
#include <vector>

using namespace concurrency;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;

namespace WindowsRuntimeAmpComponent
{
	public ref class AmpRuntimeComponent sealed
	{
	public:
		IAsyncOperation<IVectorView<float>^>^ square_array_async(IVectorView<float>^ input)
		{
			// Synchronously copy input data from host to device
			int size = input->Size;
			array<float, 1> *dataPt = new array<float, 1>(size, begin(input), end(input));

			// Asynchronously perform the computation on the GPU
			return create_async( [=]() -> IVectorView<float>^
			{
				// Array objects can only be captured by Reference
				array<float,1> &arr = *dataPt;

				// Run the kernel on the GPU
				parallel_for_each(arr.extent, [&arr] (index<1> idx) mutable restrict(amp)
				{
					arr[idx] = arr[idx] * arr[idx];
				});

				// Copy outputs from device to host
				std::vector<float> vec = std::vector<float>(size);
				copy((*dataPt), vec.begin());
				delete dataPt;

				// Return the outputs as a VectorView<float>
				return ref new Platform::Collections::VectorView<float>(vec);
			});
		}
	};
}