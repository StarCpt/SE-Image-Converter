﻿using ImageConverterPlus.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus.Services.interfaces
{
    public interface IDialogService
    {
        Task ShowAsync(IDialog dialog);
    }
}