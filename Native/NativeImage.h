#pragma once
#include "turbojpeg.h"

namespace Carnassial
{
	namespace Native
	{
		class NativeImage
		{
		private:
			// for small images the overhead of using multiple processing threads isn't particularly beneficial
			// This threshold is somewhat arbitrary regarding what might be considered worthwhile benefit for parallel operation
			// and the peformance tradeoff depends also on processor characteristics not currently considered.  As of Carnassial
			// 2.2.0.3, the purpose is to just avoid the most poor behavior.  If needed, this can be replaced with more carefully
			// scaled approaches such as per call specification of maximum concurrency and estimation of minimum pixels per
			// thread based on the processor's nominal clock frequency.
			static const __int32 MultithreadedThresholdInPixels = 2 * 1000 * 1000;

			TJPF format;
			__int32 pixelHeight;
			unsigned __int8* pixels;
			__int32 pixelSizeInBytes;
			__int32 pixelWidth;

			__int32 Abs(__int32 value);
			void AllocatePixels();

			void CombinedDifferenceScalar(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void CombinedDifferenceSse41(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void CombinedDifferenceSse41Vex(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceScalar(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceSse41(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceSse41Vex(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);

			double GetLuminosityAndColorationScalar(double* coloration, __int32 bottomRowsToSkip);
			double GetLuminosityAndColorationSse41(double* coloration, __int32 bottomRowsToSkip);
			double GetLuminosityAndColorationSse41Vex(double* coloration, __int32 bottomRowsToSkip);

			__int32 GetNumberOfCalculationBlocks(__int32 blockSizeInBytes);

		public:
			static const __int32 CalculationPixelSizeInBytes = 4;
			// must be a multiple of CalculationPixelSizeInBytes, sizeof(__m128i), and sizeof(__m256i)
			// must also be a multiple of the allocation granularity used in AllocatePixels()
			static const __int32 CombinedDifferenceTaskBlockInBytes = 192 * 1024;
			// must be aligned as CombinedDifferenceTaskBlockInBytes
			// Optimum seems to be about 256k with two threads (i5-42000).
			static const __int32 DifferenceTaskBlockInBytes = 256 * 1024;
			// preferred formats for WriteableBitmap are Bgr32 or Pbgra32 as these do not require conversion (see MSDN docs for .ctor)
			// Since SetSource() uses drives image display destinations in Carnassial to WriteableBitmap it's typically most efficient to load images
			// in a WriteableBitmap friendly format from the start.  The value here must be kept in sync with the value of PreferredPixelFormat along
			// with the implementations of Difference(), IsDark(), and so on.
			static const TJPF PreferredPixelFormat = TJPF::TJPF_BGRA;

			static void SetDefaultConcurrencyPolicy(__int32 maximumConcurrency);

			NativeImage(__int32 width, __int32 height, TJPF format, __int32 pixelSizeInBytes);
			NativeImage(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth, bool* decodeError);
			~NativeImage();

			TJPF Format()
			{
				return this->format;
			}

			__int32 GetPixelAreaSizeInBytes(__int32 rowsToSkip)
			{
				return this->pixelWidth * (this->pixelHeight - rowsToSkip) * this->pixelSizeInBytes;
			}

			__int32 PixelHeight()
			{
				return this->pixelHeight;
			}

			__int32 PixelSizeInBytes()
			{
				return this->pixelSizeInBytes;
			}

			__int32 PixelWidth()
			{
				return this->pixelWidth;
			}

			__int32 StrideInBytes()
			{
				return this->pixelWidth * this->pixelSizeInBytes;
			}

			__int32 TotalPixelBytes()
			{
				return this->TotalPixels() * this->pixelSizeInBytes;
			}

			__int32 TotalPixels()
			{
				return this->pixelHeight * this->pixelWidth;
			}

			void CopyPixelsFrom(unsigned __int8* source);
			void CopyPixelsTo(unsigned __int8* destination);
			void Difference(const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void Difference(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			double GetLuminosityAndColoration(double* coloration, __int32 bottomRowsToSkip);
			bool TryDecode(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth, bool* decodeError);
		};
	}
}