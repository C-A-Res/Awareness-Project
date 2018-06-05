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
#include <opencv2/tracking/tracking.hpp>
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
			static System::Collections::Generic::List<System::Collections::Generic::List<bool>^>^ mouthOpenRecords = gcnew System::Collections::Generic::List<System::Collections::Generic::List<bool>^>();
			static int prevNumOfFace = 0;

			static OpenCVMethods()
			{
				for (int i = 0; i < 2; i++)
				{
					mouthOpenRecords->Add(gcnew System::Collections::Generic::List<bool>());
				}
			}

			static bool compareRect1(cv::Rect face1, cv::Rect face2)
			{
				return face1.width * face1.height > face2.width * face2.height;
			}

			static bool compareRect2(cv::Rect face1, cv::Rect face2)
			{
				return face1.x < face2.x;
			}

			static ImageBuffer^ ToGray(ImageBuffer ^colorImage, ImageBuffer ^grayImage, FaceCasClassifier ^f, int% hface, int% mouthOpenPass, int% test)
			{
				cv::Mat greyMat = WrapInMat(grayImage);
				cv::Mat colorMat = WrapInMat(colorImage);
				cv::cvtColor(colorMat, greyMat, cv::COLOR_BGR2GRAY);

				std::vector<cv::Rect>  faces;
				std::vector< std::vector<cv::Point2f> > shapes;
				mouthOpenPass = -1;

				/*
				bool trackingUpdateOk;
				int prevFlag = f->facetracker->flag;

				if (prevFlag == 1)
				{
				trackingUpdateOk = f->facetracker->tracker->update(colorMat, f->facetracker->previous_bbox);
				}*/

				try
				{
					f->face_cascade->face_cas.detectMultiScale(greyMat, faces, 1.1, 3, 0 | cv::CASCADE_SCALE_IMAGE, cv::Size(60, 60));

					std::sort(faces.begin(), faces.end(), compareRect1);
					std::vector<cv::Rect> newFaces;
					for (int i = 0; i < faces.size(); i++)
					{
						double centerX = faces[i].x + 0.5 * faces[i].width;
						double centerY = faces[i].y + 0.5 * faces[i].height;
						newFaces.push_back(faces[i]);
						if (newFaces.size() == 2)
						{
							break;
						}
						/*
						if (centerX >= 80 && centerX <= 240 && centerY >= 60 && centerY <= 180)
						{

						}*/
					}

					std::sort(newFaces.begin(), newFaces.end(), compareRect2);
					faces = newFaces;

					if (prevNumOfFace != faces.size())
					{
						mouthOpenRecords[0]->Clear();
						mouthOpenRecords[1]->Clear();
						mouthOpenPass = -1;
						prevNumOfFace = faces.size();
					}
					else
					{
						if (faces.size() >= 1)
						{
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

									cv::circle(colorMat, nose_down, 1, cv::Scalar(255, 0, 0), CV_FILLED);
									cv::circle(colorMat, nose_up, 1, cv::Scalar(255, 0, 0), CV_FILLED);
									cv::circle(colorMat, lipmiddle_bottom, 1, cv::Scalar(255, 0, 0), CV_FILLED);
									cv::circle(colorMat, lipmiddle_top, 1, cv::Scalar(255, 0, 0), CV_FILLED);
									cv::circle(colorMat, lipleft_bottom, 1, cv::Scalar(255, 0, 0), CV_FILLED);
									cv::circle(colorMat, lipleft_top, 1, cv::Scalar(255, 0, 0), CV_FILLED);
									cv::circle(colorMat, lipright_bottom, 1, cv::Scalar(255, 0, 0), CV_FILLED);
									cv::circle(colorMat, lipright_top, 1, cv::Scalar(255, 0, 0), CV_FILLED);

									double dis_nose = nose_down.y - nose_up.y;
									double dis_lip_middle = lipmiddle_bottom.y - lipmiddle_top.y;
									double dis_lip_right = lipright_bottom.y - lipright_top.y;
									double dis_lip_left = lipleft_bottom.y - lipleft_top.y;

									/*
									if (prevFlag == 0)
									{
									cv::Rect2d bbox(faces[i].x, faces[i].y, faces[i].height, faces[i].width);
									f->facetracker->tracker->init(colorMat, bbox);
									f->facetracker->previous_bbox = bbox;
									f->facetracker->flag = 1;
									}

									// drawing process, combined here
									if ((prevFlag == 1 && trackingUpdateOk) || (prevFlag == 0))
									{
									rectangle(colorMat, f->facetracker->previous_bbox, cv::Scalar(0, 255, 0));
									}*/

									cv::Point p1(faces[i].x, faces[i].y);
									cv::Point p2(faces[i].x + faces[i].width, faces[i].y + faces[i].height);
									cv::Point p3(faces[i].x, faces[i].y + faces[i].height);
									// rectangle(greyMat, p1, p2, 255);
									rectangle(colorMat, p1, p2, cv::Scalar(255, 0, 255));

									bool mouthOpen = (1.0*abs(dis_nose) / (4 * abs(dis_lip_middle))) < 2.0;
									if (mouthOpenRecords[i]->Count >= 10)
									{
										mouthOpenRecords[i]->RemoveAt(0);
									}
									mouthOpenRecords[i]->Add(mouthOpen);

									if (mouthOpen)
									{
										putText(greyMat, "Mouth_Open" + std::to_string(i), p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, 255, 1);
										putText(colorMat, "Mouth_Open" + std::to_string(i), p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, cv::Scalar(255, 0, 255), 1);
										mouthOpenPass = i + 1;
									}
									else
									{
										int openFrequencyIn1s = 0;
										int nearestOpenDistance = -1;
										System::Collections::Generic::List<bool>^ temp = mouthOpenRecords[i];
										for (int j = (temp->Count) - 1; j >= 0 && (j >= (temp->Count) - 10); j--)
										{
											if (temp[j])
											{
												openFrequencyIn1s++;
												if (nearestOpenDistance == -1)
												{
													nearestOpenDistance = temp->Count - 1 - j;
												}
											}
										}

										if (1.0*openFrequencyIn1s / min(10, mouthOpenRecords[i]->Count) >= 0.7 && nearestOpenDistance > 0 && nearestOpenDistance <= 8)
										{
											putText(greyMat, "Mouth_Open" + std::to_string(i), p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, 255, 1);
											putText(colorMat, "Mouth_Open" + std::to_string(i), p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, cv::Scalar(255, 0, 255), 1);
											mouthOpenPass = i + 1;
										}
										else
										{
											putText(greyMat, "Mouth_Close" + std::to_string(i), p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, 255, 1);
											putText(colorMat, "Mouth_Close" + std::to_string(i), p3, cv::FONT_HERSHEY_SIMPLEX, 0.5, cv::Scalar(255, 0, 255), 1);
										}
									}

								}



							}
							else
							{
								mouthOpenRecords[0]->Clear();
								mouthOpenRecords[1]->Clear();
								mouthOpenPass = -1;
							}
						}
						else
						{
							mouthOpenRecords[0]->Clear();
							mouthOpenRecords[1]->Clear();
							mouthOpenPass = -1;
						}
					}

				}
				catch (std::exception e)
				{
					std::cout << "Fail, Skip!";
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