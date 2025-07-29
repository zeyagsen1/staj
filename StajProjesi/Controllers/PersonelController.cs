using StajProjesi.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;

namespace StajProjesi.Controllers
{

    public class PersonelController : Controller
    {
        IKProjectEntities db = new IKProjectEntities();


        public ActionResult Index()
        {
            return View();
        }





        [HttpPost]
        public ActionResult SaveFile(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                var fileName = Path.GetFileName(file.FileName);
                var path = Path.Combine(Server.MapPath("~/Uploads/"), fileName);

                // Ensure directory exists
                Directory.CreateDirectory(Server.MapPath("~/Uploads/"));

                file.SaveAs(path);

                // Return virtual path to preview or store
                string relativePath = Url.Content(Path.Combine("~/Uploads/", fileName));
                return Content(relativePath);
            }

            return new HttpStatusCodeResult(400, "No file uploaded");
        }



        [HttpPost]
        public JsonResult SavePersonel(Personeller personeller)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                             .SelectMany(v => v.Errors)
                             .Select(e => e.ErrorMessage).ToList();

                return Json(new { success = false, error = string.Join("; ", errors) });
            }
            try
            {
                using (var db = new IKProjectEntities())
                {
                    var yeniPersonel = new Personeller
                    {
                        Ad = personeller.Ad,
                        Soyad = personeller.Soyad,
                        DogumTarihi = personeller.DogumTarihi,
                        MedeniDurum = personeller.MedeniDurum,
                        Adres = personeller.Adres,
                        Telefon = personeller.Telefon,
                        Email = personeller.Email,
                        Cinsiyet = personeller.Cinsiyet,
                        OkulBilgileri = new List<OkulBilgileri>(),
                        IsGecmisi = new List<IsGecmisi>(),
                        Dosyalar = new List<Dosyalar>()
                    };

                    if (personeller.OkulBilgileri != null)
                    {
                        foreach (var okul in personeller.OkulBilgileri)
                        {
                            yeniPersonel.OkulBilgileri.Add(new OkulBilgileri
                            {
                                OkulAdi = okul.OkulAdi,
                                BolumAdi = okul.BolumAdi,
                                MezuniyetYili = okul.MezuniyetYili
                            });
                        }
                    }

                    if (personeller.IsGecmisi != null)
                    {
                        foreach (var isg in personeller.IsGecmisi)
                        {
                            yeniPersonel.IsGecmisi.Add(new IsGecmisi
                            {
                                FirmaAdi = isg.FirmaAdi,
                                Pozisyon = isg.Pozisyon,
                                BaslangicTarihi = isg.BaslangicTarihi,
                                BitisTarihi = isg.BitisTarihi
                            });
                        }
                    }

                    if (personeller.Dosyalar != null && personeller.Dosyalar.Any())
                    {
                        foreach (var dosya in personeller.Dosyalar)
                        {
                            yeniPersonel.Dosyalar.Add(new Dosyalar
                            {
                                DosyaAdi = dosya.DosyaAdi,
                                DosyaTuru = dosya.DosyaTuru,
                                DosyaYolu = dosya.DosyaYolu,
                                YüklenmeTarihi = DateTime.Now
                            });
                        }
                    }

                    db.Personeller.Add(yeniPersonel);
                    db.SaveChanges();

                    return Json(new { success = true });
                }
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);
                var fullErrorMessage = string.Join("; ", errorMessages);
                return Json(new { success = false, error = fullErrorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }


        public JsonResult UpdatePersonel(Personeller personel)
        {
            try
            {
                var existing = db.Personeller
                    .Include("OkulBilgileri")
                    .Include("IsGecmisi")
                    .Include("Dosyalar")
                    .FirstOrDefault(p => p.PersonelId == personel.PersonelId);

                if (existing == null)
                    return Json(new { success = false, error = "Personel bulunamadı." });

                // Ana alanları güncelle
                existing.Ad = personel.Ad;
                existing.Soyad = personel.Soyad;
                existing.DogumTarihi = personel.DogumTarihi;
                existing.MedeniDurum = personel.MedeniDurum;
                existing.Adres = personel.Adres;
                existing.Telefon = personel.Telefon;
                existing.Email = personel.Email;
                existing.Cinsiyet = personel.Cinsiyet;

                // Okul bilgilerini temizle ve güncelle
                db.OkulBilgileri.RemoveRange(existing.OkulBilgileri);
                foreach (var okul in personel.OkulBilgileri ?? new List<OkulBilgileri>())
                {
                    // Boş kayıt eklenmesin
                    if (string.IsNullOrWhiteSpace(okul.OkulAdi) && string.IsNullOrWhiteSpace(okul.BolumAdi))
                        continue;

                    existing.OkulBilgileri.Add(new OkulBilgileri
                    {
                        OkulAdi = okul.OkulAdi,
                        BolumAdi = okul.BolumAdi,
                        MezuniyetYili = okul.MezuniyetYili
                    });
                }

                // İş geçmişini temizle ve güncelle
                db.IsGecmisi.RemoveRange(existing.IsGecmisi);
                foreach (var isg in personel.IsGecmisi ?? new List<IsGecmisi>())
                {
                    // Boş kayıt eklenmesin (firma ve pozisyon kontrolü)
                    if (string.IsNullOrWhiteSpace(isg.FirmaAdi) && string.IsNullOrWhiteSpace(isg.Pozisyon))
                        continue;

                    existing.IsGecmisi.Add(new IsGecmisi
                    {
                        FirmaAdi = isg.FirmaAdi,
                        Pozisyon = isg.Pozisyon,
                        BaslangicTarihi = isg.BaslangicTarihi,
                        BitisTarihi = isg.BitisTarihi
                    });
                }

                // Dosyaları güncelle
                if (personel.Dosyalar != null && personel.Dosyalar.Any())
                {
                    db.Dosyalar.RemoveRange(existing.Dosyalar);

                    foreach (var dosya in personel.Dosyalar)
                    {
                        existing.Dosyalar.Add(new Dosyalar
                        {
                            DosyaAdi = dosya.DosyaAdi,
                            DosyaYolu = dosya.DosyaYolu,
                            DosyaTuru = dosya.DosyaTuru,
                            YüklenmeTarihi = DateTime.Now // Tarih eklenmeli
                        });
                    }
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"{x.PropertyName}: {x.ErrorMessage}");

                var fullErrorMessage = string.Join("; ", errorMessages);
                return Json(new { success = false, error = fullErrorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }


        public ActionResult Liste()
        {
            var personeller = db.Personeller.ToList();
            return View("Liste", personeller); // Views/Personel/Liste.cshtml
        }


        [HttpPost]
        public ActionResult Delete(int id)
        {
            var personel = db.Personeller.Find(id);
            if (personel != null)
            {
                db.OkulBilgileri.RemoveRange(personel.OkulBilgileri);
                db.IsGecmisi.RemoveRange(personel.IsGecmisi);
                db.Dosyalar.RemoveRange(personel.Dosyalar);
                db.Personeller.Remove(personel);
                db.SaveChanges();
            }
            return new EmptyResult();
        }

        public ActionResult Edit(int id)
        {
            var personel = db.Personeller
                            .Include("OkulBilgileri")
                            .Include("IsGecmisi")
                            .Include("Dosyalar")
                            .FirstOrDefault(p => p.PersonelId == id);
            
            if (personel == null)
            {
                return HttpNotFound();
            }
            ViewBag.dosya = personel.Dosyalar.FirstOrDefault();
            return View("Edit", personel); // Views/Personel/Edit.cshtml
        }




    }
}