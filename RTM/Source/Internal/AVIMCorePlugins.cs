﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeanCloud.Realtime.Internal
{
    internal class AVIMCorePlugins
    {
        private static readonly AVIMCorePlugins instance = new AVIMCorePlugins();
        public static AVIMCorePlugins Instance
        {
            get
            {
                return instance;
            }
        }

        private readonly object mutex = new object();

        private IAVRouterController routerController;
        public IAVRouterController RouterController
        {
            get
            {
                lock (mutex)
                {
                    routerController = routerController ?? new AVRouterController();
                    return routerController;
                }
            }
            internal set
            {
                lock (mutex)
                {
                    routerController = value;
                }
            }
        }


        private IFreeStyleMessageClassingController freeStyleClassingController;
        public IFreeStyleMessageClassingController FreeStyleClassingController
        {
            get
            {
                lock (mutex)
                {
                    freeStyleClassingController = freeStyleClassingController ?? new FreeStyleMessageClassingController();
                    return freeStyleClassingController;
                }
            }
        }
    }
}
