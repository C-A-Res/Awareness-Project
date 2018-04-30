// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

#include "stdafx.h"

namespace NU
{
	namespace Kiosk
	{
				public ref class ImageBuffer
				{
				public:
					int Width;
					int Height;
					System::IntPtr Data;
					int Stride;

					ImageBuffer(int width, int height, System::IntPtr data, int stride);
				};
	}
}
