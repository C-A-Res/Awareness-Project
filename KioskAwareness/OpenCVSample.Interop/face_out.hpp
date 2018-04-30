#pragma once
#include "stdafx.h"
#include <opencv2/core.hpp>
#include "predict_collector_out.hpp"
#include <map>

namespace cv {
	namespace facenew {
		class CV_EXPORTS_W FaceRecognizer : public Algorithm
		{
		public:
			CV_WRAP virtual void train(InputArrayOfArrays src, InputArray labels) = 0;

			CV_WRAP virtual void update(InputArrayOfArrays src, InputArray labels);

			CV_WRAP_AS(predict_label) int predict(InputArray src) const;


			CV_WRAP void predict(InputArray src, CV_OUT int &label, CV_OUT double &confidence) const;


			CV_WRAP_AS(predict_collect) virtual void predict(InputArray src, Ptr<PredictCollector> collector) const = 0;
			CV_WRAP virtual void write(const String& filename) const;

			CV_WRAP virtual void read(const String& filename);
			virtual void write(FileStorage& fs) const = 0;

	
			virtual void read(const FileNode& fn) = 0;

		
			virtual bool empty() const = 0;


			CV_WRAP virtual void setLabelInfo(int label, const String& strInfo);


			CV_WRAP virtual String getLabelInfo(int label) const;


			CV_WRAP virtual std::vector<int> getLabelsByString(const String& str) const;
			/** @brief threshold parameter accessor - required for default BestMinDist collector */
			virtual double getThreshold() const = 0;
			/** @brief Sets threshold of model */
			virtual void setThreshold(double val) = 0;
		protected:
			// Stored pairs "label id - string info"
			std::map<int, String> _labelsInfo;
		};

		//! @}

	}
}

#include "facerec_out.hpp"
#include "facemark_out.hpp"
#include "facemarkLBF_out.hpp"
#include "facemarkAAM_out.hpp"
#include "face_alignment_out.hpp"

