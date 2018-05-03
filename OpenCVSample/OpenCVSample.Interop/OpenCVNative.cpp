// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

#include "stdafx.h"
#pragma warning(push)
#pragma warning(disable : 4793)
#include <iostream>
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/core/cvdef.h>
#include <opencv2/objdetect/objdetect.hpp>
#pragma warning(pop)
#include <msclr/marshal_cppstd.h>
#include "ImageBuffer.h"
#include "FaceCasClassifier.h"

namespace NU
{
	namespace Kiosk
	{
				public ref class OpenCVMethods
				{
					// helper function

					static cv::Mat WrapInMat(ImageBuffer ^img)
					{
						cv::Mat ret = cv::Mat(img->Height, img->Width, CV_MAKETYPE(CV_8U, img->Stride / img->Width), (void *)img->Data, cv::Mat::AUTO_STEP);
						return ret;
					}


				public:
					static ImageBuffer^ ToGray( ImageBuffer ^colorImage, ImageBuffer ^grayImage, FaceCasClassifier ^f, double% dis_nose, double% dis_lip_middle, double% dis_lip_right, double% dis_lip_left, int% hface)
					{
						cv::Mat greyMat = WrapInMat(grayImage);
						cv::Mat colorMat = WrapInMat(colorImage);
						cv::cvtColor(colorMat, greyMat, cv::COLOR_BGR2GRAY);

						std::vector<cv::Rect> faces;
						std::vector< std::vector<cv::Point2f> > shapes;

						f->face_cascade->face_cas.detectMultiScale(greyMat, faces, 1.1, 3, 0 | cv::CASCADE_SCALE_IMAGE, cv::Size(60,60));

						if (faces.size() == 1) {
							hface = 1;
							
							if (f->facemark->face_landmark->fit(colorMat, faces, shapes))
							{
								for (int i = 0; i < faces.size(); i++)
								{
									cv::Point2f nose_down = shapes[i][30];
									cv::Point2f nose_up = shapes[i][27];
									cv::Point2f lipmiddle_bottom = shapes[i][66];
									cv::Point2f lipmiddle_top = shapes[i][62];
									cv::Point2f lipleft_bottom = shapes[i][67];
									cv::Point2f lipleft_top = shapes[i][61];
									cv::Point2f lipright_bottom = shapes[i][65];
									cv::Point2f lipright_top = shapes[i][63];
									cv::circle(greyMat, nose_down, 1, 255, CV_FILLED);
									cv::circle(greyMat, nose_up, 1, 255, CV_FILLED);
									cv::circle(greyMat, lipmiddle_bottom, 1, 255, CV_FILLED);
									cv::circle(greyMat, lipmiddle_top, 1, 255, CV_FILLED);
									cv::circle(greyMat, lipleft_bottom, 1, 255, CV_FILLED);
									cv::circle(greyMat, lipleft_top, 1, 255, CV_FILLED);
									cv::circle(greyMat, lipright_bottom, 1, 255, CV_FILLED);
									cv::circle(greyMat, lipright_top, 1, 255, CV_FILLED);

									dis_nose = nose_down.y - nose_up.y;

									dis_lip_middle = lipmiddle_bottom.y - lipmiddle_top.y;

									dis_lip_right = lipright_bottom.y - lipright_top.y;

									dis_lip_left = lipleft_bottom.y - lipleft_top.y;
								}
							}
							else {
								dis_nose = 0.0;
								dis_lip_middle = 0.0;
								dis_lip_right = 0.0;
								dis_lip_left = 0.0;
							}
							for (int i = 0; i < faces.size(); i++)
							{
								cv::Point p1(faces[i].x, faces[i].y);
								cv::Point p2(faces[i].x + faces[i].width, faces[i].y + faces[i].height);
								cv::Point p3(faces[i].x, faces[i].y + faces[i].height);
								rectangle(greyMat, p1, p2, 255);
								if ((abs(dis_nose) / (4 * abs(dis_lip_middle))) < 3) {
									putText(greyMat, "Mouth_Open", p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, 255, 1);
								}
								else
								{
									putText(greyMat, "Mouth_Close", p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, 255, 1);
								}
								
							}
						}
						else {
							dis_nose = 0.0;
							dis_lip_middle = 0.0;
							dis_lip_right = 0.0;
							dis_lip_left = 0.0;
						}

						return grayImage;
					}

					static void SaveImage(ImageBuffer ^img, System::String ^filename)
					{
						std::string fn = msclr::interop::marshal_as<std::string>(filename);
						cv::Mat matImg = WrapInMat(img);
						cv::imwrite(fn, matImg);
						delete &matImg;
					}
				};
	}
}