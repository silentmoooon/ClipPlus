﻿namespace ClipOne.model
{
    public class ClipModel
    {
        /// <summary>
        /// 数据类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public string ClipValue { get; set; }

        /// <summary>
        ///  显示的值
        /// </summary>
        public string DisplayValue { get; set; }


        /// <summary>
        /// 原始文字,供html、QQ、WECHAT类型使用
        /// </summary>
        public string PlainText { get; set; }

      
        
    }
}
